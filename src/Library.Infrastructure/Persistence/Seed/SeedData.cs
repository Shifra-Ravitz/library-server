using Library.Domain.Entities;
using Library.Domain.Enums;

namespace Library.Infrastructure.Persistence.Seed;

/// <summary>
/// Static reference data inserted through HasData in the entity configurations.
/// Every value here is fixed, so the model snapshot does not change between migrations.
/// Navigation properties are never set: HasData accepts foreign keys only.
/// </summary>
internal static class SeedData
{
    public const int PlanBasicId = 1;
    public const int PlanStandardId = 2;
    public const int PlanPremiumId = 3;

    public static readonly MembershipPlan[] MembershipPlans =
    [
        new() { Id = PlanBasicId,    Name = "בסיסי", MaxConcurrentLoans = 1, Price = 0m  },
        new() { Id = PlanStandardId, Name = "רגיל",  MaxConcurrentLoans = 3, Price = 30m },
        new() { Id = PlanPremiumId,  Name = "מורחב", MaxConcurrentLoans = 5, Price = 50m },
    ];

    public static readonly Category[] Categories =
    [
        new() { Id = 1, Name = "הלכה" },
        new() { Id = 2, Name = "מוסר" },
        new() { Id = 3, Name = "אמונה והשקפה" },
        new() { Id = 4, Name = "תולדות ישראל" },
        new() { Id = 5, Name = "ספרי יסוד" },
    ];

    public static readonly Book[] Books =
    [
        new() { Id = 1,  Title = "מסילת ישרים",      Author = "רבי משה חיים לוצאטו (הרמח\"ל)", PublicationYear = 1740, IsDeleted = false },
        new() { Id = 2,  Title = "חובות הלבבות",     Author = "רבנו בחיי אבן פקודה",           PublicationYear = 1489, IsDeleted = false },
        new() { Id = 3,  Title = "הכוזרי",           Author = "רבי יהודה הלוי",                PublicationYear = 1506, IsDeleted = false },
        new() { Id = 4,  Title = "חפץ חיים",         Author = "רבי ישראל מאיר הכהן",           PublicationYear = 1873, IsDeleted = false },
        new() { Id = 5,  Title = "שמירת הלשון",      Author = "רבי ישראל מאיר הכהן",           PublicationYear = 1876, IsDeleted = false },
        new() { Id = 6,  Title = "משנה ברורה",       Author = "רבי ישראל מאיר הכהן",           PublicationYear = 1884, IsDeleted = false },
        new() { Id = 7,  Title = "קיצור שולחן ערוך", Author = "רבי שלמה גנצפריד",              PublicationYear = 1864, IsDeleted = false },
        new() { Id = 8,  Title = "דרך ה'",           Author = "רבי משה חיים לוצאטו (הרמח\"ל)", PublicationYear = 1896, IsDeleted = false },
        new() { Id = 9,  Title = "סדר הדורות",       Author = "רבי יחיאל היילפרין",            PublicationYear = 1769, IsDeleted = false },
        new() { Id = 10, Title = "צמח דוד",          Author = "רבי דוד גנז",                   PublicationYear = 1592, IsDeleted = false },
    ];

    // Rows of the BookCategories join table (shared-type entity "BookCategory").
    public static readonly object[] BookCategories =
    [
        new { BookId = 1,  CategoryId = 2 }, new { BookId = 1,  CategoryId = 5 },
        new { BookId = 2,  CategoryId = 2 }, new { BookId = 2,  CategoryId = 3 },
        new { BookId = 3,  CategoryId = 3 }, new { BookId = 3,  CategoryId = 5 },
        new { BookId = 4,  CategoryId = 1 }, new { BookId = 4,  CategoryId = 2 },
        new { BookId = 5,  CategoryId = 2 },
        new { BookId = 6,  CategoryId = 1 }, new { BookId = 6,  CategoryId = 5 },
        new { BookId = 7,  CategoryId = 1 },
        new { BookId = 8,  CategoryId = 3 },
        new { BookId = 9,  CategoryId = 4 },
        new { BookId = 10, CategoryId = 4 },
    ];

    // 3 עותקים לספר אי-זוגי, 2 לזוגי: 25 עותקים. Id רץ מ-1 עד 25.
    public static readonly BookCopy[] BookCopies = BuildCopies();

    private static BookCopy[] BuildCopies()
    {
        var copies = new List<BookCopy>();
        var copyId = 1;
        foreach (var book in Books)
        {
            var count = book.Id % 2 == 1 ? 3 : 2;
            for (var i = 0; i < count; i++, copyId++)
            {
                copies.Add(new BookCopy
                {
                    Id = copyId,
                    BookId = book.Id,
                    Status = CopyStatus.Available,
                    Version = FixedVersion(copyId),
                });
            }
        }
        return [.. copies];
    }

    // Version קבוע שנגזר מה-Id, כדי שה-seed לא ישתנה בין migrations
    private static Guid FixedVersion(int id) => new($"00000000-0000-0000-0000-{id:D12}");
}
