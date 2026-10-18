using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration;

[Trait("Category", "Integration")]
public class ConcurrencyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _db;

    public ConcurrencyTests(PostgresFixture db) => _db = db;

    [Fact]
    public async Task SaveChanges_TwoContextsLoanSameCopy_SecondThrowsConcurrencyException()
    {
        // Arrange: ספר חדש עם עותק Available אחד (נתונים משלה, לא ה-seed)
        var (_, copyIds) = await SeedBookAsync(copies: 1);
        var copyId = copyIds[0];

        // שני contexts נפרדים = שתי לקוחות שלוחצות "השאל" באותו רגע
        await using var ctxA = _db.CreateContext();
        await using var ctxB = _db.CreateContext();

        // שתיהן קוראות את העותק לפני שמישהי שמרה: שתיהן רואות Available ואותו Version
        var copyA = await ctxA.BookCopies.SingleAsync(c => c.Id == copyId);
        var copyB = await ctxB.BookCopies.SingleAsync(c => c.Id == copyId);
        Assert.Equal(CopyStatus.Available, copyA.Status);
        Assert.Equal(CopyStatus.Available, copyB.Status);
        Assert.Equal(copyA.Version, copyB.Version);
        var originalVersion = copyA.Version;

        // שתיהן עוברות את הבדיקה העסקית ומשנות ל-Loaned
        copyA.Status = CopyStatus.Loaned;
        copyB.Status = CopyStatus.Loaned;

        // Act 1: הראשונה שומרת בהצלחה. הדריסה של SaveChangesAsync מחליפה את ה-Version
        await ctxA.SaveChangesAsync();
        var versionAfterFirst = copyA.Version;
        Assert.NotEqual(originalVersion, versionAfterFirst);

        // Act 2 + Assert: השנייה שולחת UPDATE עם ה-Version הישן ב-WHERE,
        // אף שורה לא מתעדכנת, ו-EF זורק
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctxB.SaveChangesAsync());

        // Assert: context שלישי וטרי רואה את מה שהראשונה שמרה
        await using var ctxCheck = _db.CreateContext();
        var saved = await ctxCheck.BookCopies.AsNoTracking().SingleAsync(c => c.Id == copyId);
        Assert.Equal(CopyStatus.Loaned, saved.Status);
        Assert.Equal(versionAfterFirst, saved.Version);
    }

    [Fact]
    public async Task SaveChanges_TwoContextsExceedQuotaWithDifferentCopies_SecondThrowsAndRollsBack()
    {
        // Arrange: לקוחה שנשארה לה השאלה אחת בדיוק, ושני עותקים פנויים ושונים
        var (_, copyIds) = await SeedBookAsync(copies: 2);
        var (userId, max) = await SeedMemberOneBelowQuotaAsync();

        await using var ctxA = _db.CreateContext();
        await using var ctxB = _db.CreateContext();

        // כל בקשה קוראת את אותה לקוחה, ועותק אחר
        var userA = await ctxA.Users.SingleAsync(u => u.Id == userId);
        var userB = await ctxB.Users.SingleAsync(u => u.Id == userId);
        var copyA = await ctxA.BookCopies.SingleAsync(c => c.Id == copyIds[0]);
        var copyB = await ctxB.BookCopies.SingleAsync(c => c.Id == copyIds[1]);
        var copyBOriginalVersion = copyB.Version;

        // שתיהן רואות ActiveLoansCount = max-1, ולכן שתיהן עוברות את בדיקת המכסה
        Assert.True(userA.ActiveLoansCount < max);
        Assert.True(userB.ActiveLoansCount < max);
        userA.ActiveLoansCount++;
        copyA.Status = CopyStatus.Loaned;
        userB.ActiveLoansCount++;
        copyB.Status = CopyStatus.Loaned;

        // Act 1: הראשונה שומרת: העותק שלה וה-User מתעדכנים באותה שמירה
        await ctxA.SaveChangesAsync();
        var userVersionAfterFirst = userA.Version;

        // Act 2 + Assert: העותק של השנייה היה פנוי, אבל ה-Version של User כבר השתנה
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctxB.SaveChangesAsync());

        // Assert: המכסה לא נפרצה, והעותק של הבקשה שנכשלה לא נשאר "תפוס",
        // כי כל ה-SaveChanges רץ בטרנזקציה אחת ובוטל כולו
        await using var ctxCheck = _db.CreateContext();
        var user = await ctxCheck.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        var savedA = await ctxCheck.BookCopies.AsNoTracking().SingleAsync(c => c.Id == copyIds[0]);
        var savedB = await ctxCheck.BookCopies.AsNoTracking().SingleAsync(c => c.Id == copyIds[1]);
        Assert.Equal(max, user.ActiveLoansCount);
        Assert.Equal(userVersionAfterFirst, user.Version);
        Assert.Equal(CopyStatus.Loaned, savedA.Status);
        Assert.Equal(CopyStatus.Available, savedB.Status);
        Assert.Equal(copyBOriginalVersion, savedB.Version);
    }

    [Fact]
    public async Task SaveChanges_SoftDeleteBookWhileLoaningItsCopy_SecondThrowsConcurrencyException()
    {
        // Arrange: ספר עם עותק Available אחד
        var (bookId, copyIds) = await SeedBookAsync(copies: 1);
        var copyId = copyIds[0];

        await using var ctxLibrarian = _db.CreateContext();
        await using var ctxMember = _db.CreateContext();

        // הספרנית טוענת את הספר עם העותקים כדי למחוק לוגית, הלקוחה טוענת את העותק כדי לשאול
        var book = await ctxLibrarian.Books.Include(b => b.Copies).SingleAsync(b => b.Id == bookId);
        var memberCopy = await ctxMember.BookCopies.SingleAsync(c => c.Id == copyId);

        book.IsDeleted = true;
        foreach (var c in book.Copies) c.Status = CopyStatus.Retired;
        memberCopy.Status = CopyStatus.Loaned;

        // Act 1: המחיקה הלוגית נשמרת ומחליפה את ה-Version של העותק
        await ctxLibrarian.SaveChangesAsync();

        // Act 2 + Assert: ההשאלה מתנגשת על אותו עותק
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctxMember.SaveChangesAsync());

        // Assert: הספר מחוק והעותק Retired. אין מצב של ספר מחוק עם עותק מושאל
        await using var ctxCheck = _db.CreateContext();
        var savedBook = await ctxCheck.Books.AsNoTracking().SingleAsync(b => b.Id == bookId);
        var savedCopy = await ctxCheck.BookCopies.AsNoTracking().SingleAsync(c => c.Id == copyId);
        Assert.True(savedBook.IsDeleted);
        Assert.Equal(CopyStatus.Retired, savedCopy.Status);
    }

    // ---------- עזר ----------

    // כל בדיקה יוצרת ספר משלה, כדי שהבדיקות לא ישפיעו זו על זו (הקונטיינר משותף למחלקה)
    private async Task<(int BookId, int[] CopyIds)> SeedBookAsync(int copies)
    {
        await using var db = _db.CreateContext();
        var book = new Book
        {
            Title = $"Race test {Guid.NewGuid():N}",   // שמות השדות של Book: לפי ה-Domain בפועל
            Author = "Test",
            PublicationYear = 2020,
            IsDeleted = false
        };
        for (var i = 0; i < copies; i++)
            book.Copies.Add(new BookCopy { Status = CopyStatus.Available, Version = Guid.NewGuid() });

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return (book.Id, book.Copies.Select(c => c.Id).ToArray());
    }

    // לקוחה עם מנוי מה-seed, שמחזיקה max-1 ספרים: מותרת לה בדיוק עוד השאלה אחת
    private async Task<(int UserId, int Max)> SeedMemberOneBelowQuotaAsync()
    {
        await using var db = _db.CreateContext();
        var plan = await db.MembershipPlans
            .Where(p => p.MaxConcurrentLoans >= 2)
            .OrderBy(p => p.MaxConcurrentLoans)
            .FirstAsync();

        var user = new User
        {
            Name = "Race Test Member",
            Email = $"race-{Guid.NewGuid():N}@test.local", // אינדקס ייחודי על Email
            PasswordHash = "not-a-real-hash",
            Role = UserRole.Member,
            MembershipPlanId = plan.Id,
            ActiveLoansCount = plan.MaxConcurrentLoans - 1,
            Version = Guid.NewGuid()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, plan.MaxConcurrentLoans);
    }
}
