# אוצר הספרים – שרת ניהול ספרייה

## 1. על המערכת

שרת ASP.NET Core Web API (net8.0) לניהול ספרייה. קוראים רשומים (role `Member`) מחפשים ספרים, שואלים עותקים ומחזירים אותם, לפי מכסת ההשאלות של המנוי שלהם. ספרן (role `Librarian`) מנהל את הקטלוג: ספרים, קטגוריות ועותקים, ורואה את כל ההשאלות. בסיס הנתונים הוא PostgreSQL דרך EF Core (Npgsql), וההזדהות היא ב-JWT.

## 2. המשאב המוגבל והתחרות עליו

**המשאב המוגבל** הוא העותק הפיזי (`BookCopy`): לכל עותק יכולה להיות השאלה פעילה אחת בלבד. משאב מוגבל נוסף הוא מכסת ההשאלות של המשתמש: `User.ActiveLoansCount` מושווה ל-`MembershipPlan.MaxConcurrentLoans` של המנוי שלו.

**איפה התחרות:**
- שני משתמשים לוחצים "השאלה" על העותק האחרון באותו רגע. שתי הבקשות קוראות את העותק כ-`Available`, ושתיהן עוברות את הבדיקה העסקית.
- משתמש שנשארה לו השאלה אחת במכסה שולח שתי בקשות השאלה במקביל, על שני עותקים שונים. שתי הבקשות רואות `ActiveLoansCount` מתחת למקסימום.
- ספרן מוחק ספר (מחיקה לוגית) בזמן שקורא שואל עותק שלו.

**איך המערכת מזהה:** ל-`BookCopy` ול-`User` יש שדה `Guid Version` (הממשק `IHasVersion` ב-`src/Library.Domain/Common/IHasVersion.cs`). הוא מוגדר כ-concurrency token עם `IsConcurrencyToken()` ב-`src/Library.Infrastructure/Persistence/Configurations/BookCopyConfiguration.cs` וב-`UserConfiguration.cs`. הדריסה של `SaveChangesAsync` ב-`src/Library.Infrastructure/Persistence/AppDbContext.cs` (המתודה `UpdateVersions`) מחליפה את ה-`Version` בערך חדש בכל שמירה של ישות שהשתנתה. EF מוסיף את הערך הישן לתנאי ה-`WHERE` של ה-`UPDATE`, כך שאם בקשה אחרת כבר שמרה, אף שורה לא מתעדכנת. בהשאלה, רשומת ההשאלה החדשה, שינוי הסטטוס של העותק והגדלת המונה של המשתמש נשמרים ב-`SaveChanges` אחד, ולכן בהתנגשות כל השמירה מבוטלת.

**איך המערכת מגיבה:**
1. `DbUpdateConcurrencyException` נתפסת במקום אחד בלבד: `UnitOfWork` ב-Infrastructure, בקובץ `src/Library.Infrastructure/Persistence/Repositories/UnitOfWork.cs`. שם היא מתורגמת ל-`ConcurrencyConflictException` (`src/Library.Application/Common/Exceptions/ConcurrencyConflictException.cs`), כדי ששכבת Application לא תכיר את EF.
2. ה-service שקרא לשמירה (`LoanService.BorrowAsync` ו-`ReturnAsync`, `BookService.DeleteAsync`, `BookCopyService.DeleteAsync`) תופס את `ConcurrencyConflictException`, כותב ללוג ברמת `Warning` (בלי stack trace, כי זה מרוץ צפוי ולא באג) ומחזיר `Error.Conflict` עם הודעה ברורה בעברית.
3. `ApiControllerBase.ToProblem` מתרגם `ErrorType.Conflict` ל-`409 Conflict` עם גוף ProblemDetails, שה-`detail` שלו הוא ההודעה.

**ההוכחה:** `tests/Library.Tests/Integration/ConcurrencyTests.cs` רץ מול PostgreSQL אמיתי (`postgres:16`) שמורם ב-Testcontainers דרך `PostgresFixture.cs`, שמריץ את אותם migrations שהשרת מריץ. כל בדיקה פותחת שני `AppDbContext` נפרדים, כמו שתי בקשות HTTP:
- `SaveChanges_TwoContextsLoanSameCopy_SecondThrowsConcurrencyException`: שני contexts משאילים את אותו עותק; הראשון נשמר וה-`Version` מתחלף, השני נכשל, והעותק נשאר עם הנתונים של הראשון.
- `SaveChanges_TwoContextsExceedQuotaWithDifferentCopies_SecondThrowsAndRollsBack`: משתמש במרחק השאלה אחת מהמכסה שואל במקביל שני עותקים שונים; השני נכשל על ה-`Version` של `User`, המכסה לא נפרצת, והעותק השני נשאר `Available`.
- `SaveChanges_SoftDeleteBookWhileLoaningItsCopy_SecondThrowsConcurrencyException`: מחיקה לוגית של ספר מתנגשת עם השאלה של העותק שלו; אין מצב של ספר מחוק עם עותק מושאל.

**Optimistic מול Pessimistic:** נבחרה Optimistic concurrency, כי התנגשויות על אותו עותק או אותו משתמש נדירות ביחס למספר הבקשות. אין צורך להחזיק נעילה על שורה בזמן הבקשה, אין סיכון ל-deadlock, ובמקרה הנדיר של התנגשות המשתמש מקבל 409 ומנסה שוב.

## 3. הרצה מקומית

**דרישות:** .NET 8 SDK, Docker Desktop.

**הרמת PostgreSQL:**

```powershell
docker run --name library-postgres -e POSTGRES_USER=library -e POSTGRES_PASSWORD=<DB_PASSWORD> -e POSTGRES_DB=librarydb -p 5432:5432 -d postgres:16
```

**הגדרת User Secrets** (ה-`UserSecretsId` כבר מוגדר ב-`Library.API.csproj`):

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=librarydb;Username=library;Password=<DB_PASSWORD>" --project src\Library.API
dotnet user-secrets set "Jwt:Key" "<RANDOM_KEY_AT_LEAST_32_BYTES>" --project src\Library.API
```

- `ConnectionStrings:Default`: חובה. בלעדיו השרת לא עולה.
- `Jwt:Key`: חובה, 32 בתים לפחות. בלעדיו `ValidateOnStart` עוצר את העלייה.
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes`, `LoanSettings:LoanPeriodDays` ו-`Cors:AllowedOrigins` כבר מוגדרים ב-`appsettings.json`.
- `AutoMapper:LicenseKey`: לא חובה. אם יש רישיון, מגדירים אותו באותה דרך: `dotnet user-secrets set "AutoMapper:LicenseKey" "<AUTOMAPPER_LICENSE_KEY>" --project src\Library.API`.

**הרצה:**

```powershell
dotnet run --project src\Library.API
```

ה-migrations וה-seed רצים אוטומטית בעליית השרת (`DbInitializer.InitializeAsync`, שנקרא מ-`Program.cs`), ואין צורך בשום פעולה ידנית בבסיס הנתונים.

**Swagger:** `http://localhost:5150/swagger` (בפרופיל `https`: `https://localhost:7050/swagger`). Swagger פעיל בכל הסביבות, כולל Production ב-Render, לצורך הדגמה.

**שימוש ב-Authorize:**
1. מריצים `POST /api/auth/login` עם אימייל וסיסמה של אחד ממשתמשי הדמו (סעיף 5).
2. מעתיקים את הערך של `token` מהתשובה.
3. לוחצים על הכפתור Authorize, מדביקים רק את ה-token, בלי המילה `Bearer`, ולוחצים Authorize.

הלוגים נכתבים לקונסול ולקובץ `src\Library.API\bin\Debug\net8.0\logs\library-YYYY-MM-DD.log` (לפי `nlog.config`).

## 4. בדיקות

כל הבדיקות (בדיקות האינטגרציה דורשות Docker Desktop פועל):

```powershell
dotnet test tests\Library.Tests\Library.Tests.csproj
```

unit tests בלבד, בלי Docker:

```powershell
dotnet test tests\Library.Tests\Library.Tests.csproj --filter "Category!=Integration"
```

ה-unit tests (`tests/Library.Tests/Services`) בודקים את `LoanService` ואת `BookService` עם xUnit ו-Moq, כשה-repositories וה-`IUnitOfWork` הם mocks. הם בודקים את המקרה התקין של המשאב המוגבל (השאלה של עותק פנוי מתחת למכסה) ואת המקרים שנדחים: עותק מושאל, מכסה מלאה, השאלה באיחור, עותק Retired, ספר מחוק, ו-`ConcurrencyConflictException` בשמירה שמוחזרת כ-Conflict.

## 5. משתמשי דמו

נוצרים ב-`src/Library.Infrastructure/Persistence/DbInitializer.cs` בעליית השרת, אם הם לא קיימים. הסיסמה היא סיסמת דמו ציבורית בכוונה.

| role | אימייל | סיסמה | מנוי |
|---|---|---|---|
| Librarian | `librarian@library.local` | `Demo123!` | אין |
| Member | `member1@library.local` | `Demo123!` | בסיסי |
| Member | `member2@library.local` | `Demo123!` | רגיל |

סוגי המנויים (`src/Library.Infrastructure/Persistence/Seed/SeedData.cs`):

| מנוי | מקסימום ספרים בו-זמנית |
|---|---|
| בסיסי | 1 |
| רגיל | 3 |
| מורחב | 5 |

## 6. קישורים

- הקליינט (React + Vite): https://github.com/Shifra-Ravitz/library-client
- שרת ב-Render: יתווסף לאחר ההעלאה
- קליינט ב-Render: יתווסף לאחר ההעלאה

### העלאה ל-Render

ב-Render מוגדרים שלושה שירותים: PostgreSQL לבסיס הנתונים, Web Service מסוג Docker לשרת, שנבנה לפי ה-`Dockerfile` שבתיקייה הראשית של ה-repository, ו-Static Site לקליינט. ה-migrations וה-seed רצים אוטומטית בעליית השרת, כמו בהרצה מקומית, ואין צורך להריץ שום דבר ידנית מול בסיס הנתונים.

משתני הסביבה של ה-Web Service. הערכים מוגדרים רק ב-Render, ולא נשמרים ב-repository או ב-`Dockerfile`:

| משתנה | ערך |
|---|---|
| `ConnectionStrings__Default` | חובה. בפורמט Npgsql: `Host=<HOST>;Port=5432;Database=<DB>;Username=<USER>;Password=<PASSWORD>` |
| `Jwt__Key` | חובה. מפתח אקראי של 32 תווים לפחות |
| `Cors__AllowedOrigins__0` | הכתובת של הקליינט ב-Render, בלי `/` בסוף |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AutoMapper__LicenseKey` | לא חובה |

## 7. מבנה הפרויקטים

| פרויקט | מה יושב בו | מפנה ל- |
|---|---|---|
| `src/Library.Domain` | ישויות (`Book`, `BookCopy`, `User`, `Loan`, `MembershipPlan`, `Category`), enums, `IHasVersion` | אף פרויקט |
| `src/Library.Application` | ממשקי repositories ו-services, `IUnitOfWork`, DTOs, services, פרופילי AutoMapper, `Result`/`Error`, חריגות, options | Domain |
| `src/Library.Infrastructure` | `AppDbContext`, configurations, migrations, seed, repositories, `UnitOfWork`, BCrypt ו-JWT | Application, Domain |
| `src/Library.API` | controllers, middleware, `Program.cs`, קונפיגורציה, `nlog.config` | Application, Infrastructure |
| `tests/Library.Tests` | unit tests עם xUnit ו-Moq, בדיקות אינטגרציה עם Testcontainers | Domain, Application, Infrastructure |

התאמה לחלוקה מהקורס:

| בקורס | בפרויקט |
|---|---|
| Core | `Library.Domain` + הממשקים וה-DTOs ב-`Library.Application` |
| Data | `Library.Infrastructure` |
| Service | ה-Services ב-`Library.Application` |
| API | `Library.API` |

הפרויקט בנוי לפי החלוקה החדשה של Clean Architecture. אף אחד מ-Domain ו-Application לא מפנה ל-EF Core: הממשקים מוגדרים ב-Application, והמימוש שתלוי ב-EF נמצא רק ב-Infrastructure.

## 8. מחיקה לוגית

ספרים ועותקים לא נמחקים פיזית, כדי לשמור את היסטוריית ההשאלות שמפנה אליהם.
- מחיקת עותק (`BookCopyService.DeleteAsync`) מעבירה אותו ל-`Retired`. עותק מושאל לא ניתן למחיקה.
- מחיקת ספר (`BookService.DeleteAsync`) מסמנת `IsDeleted` ומעבירה את כל העותקים שלו ל-`Retired`, באותה שמירה. ספר שיש לו עותק מושאל לא ניתן למחיקה.

עותק `Retired` או עותק של ספר מחוק לא ניתן להשאלה. זה נבדק ב-`LoanService.BorrowAsync`, שמחזיר 404 גם אם הקליינט שולח את ה-id של עותק כזה ישירות.
