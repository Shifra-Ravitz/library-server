# שלב 17: בדיקות תחרות עם שני DbContext

## קבצים שנוצרו או שונו

- `tests/Library.Tests/Library.Tests.csproj` (שונה):
  - נוסף `ProjectReference` ל-`src\Library.Infrastructure\Library.Infrastructure.csproj`
  - נוספה החבילה `Testcontainers.PostgreSql` בגרסה `4.16.0`
  - גרסאות החבילות הקיימות לא השתנו
- `tests/Library.Tests/Integration/PostgresFixture.cs` (חדש)
- `tests/Library.Tests/Integration/ConcurrencyTests.cs` (חדש)
- `stage17-notes.md` (הקובץ הזה; נוצר אחרי ה-commit ולא נכלל בו)

אין שום שינוי ב-`src`, ולא נוצר migration.

## שינויים לעומת הקוד שקיבלתי

שינוי אחד בלבד:

| מקום | בקוד שקיבלתי | בקוד בפועל | למה |
|---|---|---|---|
| `ConcurrencyTests.SeedBookAsync` | `PublishYear = 2020` | `PublicationYear = 2020` | ב-`Library.Domain/Entities/Book.cs` השדה נקרא `PublicationYear` |

כל השאר נבדק ותאם לקוד הקיים, ולכן לא שונה:

- namespace של AppDbContext: `Library.Infrastructure.Persistence` ✓
- namespaces של הישויות ושל ה-enums: `Library.Domain.Entities`, `Library.Domain.Enums` ✓
- שמות ה-DbSet: `Users`, `MembershipPlans`, `Books`, `BookCopies` ✓
- Book: `Title`, `Author`, `IsDeleted`, ו-collection בשם `Copies` (מאותחל ל-`new List<BookCopy>()`) ✓
- ה-constructor של AppDbContext מקבל `DbContextOptions<AppDbContext>` ✓
- xUnit בגרסה 2.5.3 (v2), ולכן `IAsyncLifetime` מחזיר `Task` ✓
- ל-`BookCopy.Version` ול-`User.Version` יש `get; set;` ציבורי ✓
- אין שדות חובה נוספים: ל-User, Book ו-BookCopy אין שדות תאריך. ל-BookCopy יש `BookId`, ש-EF ממלא דרך `book.Copies`
- ב-seed יש מנוי עם `MaxConcurrentLoans >= 2` ("רגיל", 3), ולכן `FirstAsync` מוצא תוכנית

## תוצאות ההרצות (סעיף 4)

| שלב | פקודה | תוצאה |
|---|---|---|
| א | `docker info` | Docker Desktop פועל (שרת 29.8.2) |
| ב | `dotnet test` (הכול) | 17 עברו, 0 נכשלו |
| ג | `--filter "Category=Integration"` | 3 עברו, 0 נכשלו |
| ג | `--filter "Category!=Integration"` | 14 עברו, 0 נכשלו |
| ד | שבירה: `.IsConcurrencyToken()` ב-BookCopyConfiguration הפך להערה | 1 עברה, 2 נכשלו |
| ה | שבירה: `.IsConcurrencyToken()` של Version ב-UserConfiguration הפך להערה | 2 עברו, 1 נכשלה |
| ז | `dotnet test` (הכול), אחרי החזרת השורות | 17 עברו, 0 נכשלו |

### ניסוי שבירה ד (BookCopy)

נכשלו, כמו שציפינו:
- `SaveChanges_TwoContextsLoanSameCopy_SecondThrowsConcurrencyException`
- `SaveChanges_SoftDeleteBookWhileLoaningItsCopy_SecondThrowsConcurrencyException`

בשתיהן השגיאה היא `Assert.Throws() Failure: No exception was thrown`: בלי ה-token, ה-UPDATE השני לא כולל את Version ב-WHERE ודורס את השמירה הראשונה.
בדיקת המכסה עברה, כי שם ההתנגשות היא על User.

### ניסוי שבירה ה (User)

נכשלה, כמו שציפינו:
- `SaveChanges_TwoContextsExceedQuotaWithDifferentCopies_SecondThrowsAndRollsBack` (`No exception was thrown`)

שתי הבדיקות על העותק עברו.

### החזרת השורות (שלב ו)

אחרי כל ניסוי השורה הוחזרה בדיוק כמו שהייתה. `git diff src` היה ריק, ו-`git status` הראה רק את ה-csproj ואת תיקיית `Integration` בתיקיית הבדיקות.

## commit

כן. commit `f7dee99` עם ההודעה `Stage 17: concurrency integration tests with Testcontainers`.
הוא כולל את ה-csproj ואת שני קובצי הבדיקות. קובץ ההערות הזה לא נכלל בו.

## מה עוד לא נבדק

- הבדיקות עובדות ישירות מול `AppDbContext` ולא דרך `LoanService` או ה-repositories. לכן הן לא בודקות שהשירות באמת מעדכן את `ActiveLoansCount` ואת העותק באותה שמירה, ולא את ההמרה של `DbUpdateConcurrencyException` ל-`ConcurrencyConflictException` ול-409.
- אין בדיקת API מקצה לקצה (למשל עם `WebApplicationFactory`) על שתי בקשות HTTP במקביל.
- הבדיקות מדמות תחרות בסדר קבוע (A שומרת ואז B). אין הרצה מקבילית אמיתית עם `Task.WhenAll`.
- החזרת ספר (Return) במקביל להשאלה, ו-`ActiveLoansCount` שיורד מתחת ל-0 (ה-check constraint), לא נבדקו.
- הבדיקות צריכות Docker פועל. אם Docker סגור, 3 בדיקות האינטגרציה ייכשלו ב-`InitializeAsync`, ולא ידלגו.
