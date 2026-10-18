# שלב 10: הרשמה, התחברות ו-JWT

הקוד לא קומפל בזמן הכתיבה: לא הורצו `dotnet build`, `dotnet run` או `dotnet ef`. לא הותקנו חבילות, אין שינוי במודל ואין migration.

## מה כבר היה קיים ולא שוכפל

- **`IUserRepository`:** ‏`GetByEmailAsync` (עם AsNoTracking) ו-`EmailExistsAsync` (עם AnyAsync) כבר קיימות. במקום `AddAsync` יש `void Add(User user)`, ובה נעשה שימוש. היא לא קוראת ל-SaveChanges.
- **`IMembershipPlanRepository.ExistsAsync`:** כבר קיימת, עם AnyAsync.
- **`RegisterRequest`:** ‏`MembershipPlanId` הוא `int` ועליו כבר `[Range(1, int.MaxValue)]`. לא היה שדה Role. ל-Email כבר היה `[StringLength(256)]`, בהתאם ל-`UserConfiguration`.
- **`LoginRequest`:** כבר היה `[Required]` על שני השדות. לא שונה.
- **`DbInitializer`:** האימיילים של משתמשי הדמו כבר באותיות קטנות, ולספרנית כבר יש `UserRole.Librarian`. לא שונה. ה-`TODO (stage 10)` שבו (החלפת הקריאה הישירה ל-BCrypt ב-`IPasswordHasher`) נשאר, כי אופן יצירת ה-hash לא היה אמור להשתנות. ברירת המחדל של `BCrypt.HashPassword` היא work factor 11, כך שה-hash-ים זהים לאלה של `BCryptPasswordHasher`.

## קבצים שנוצרו

| קובץ | מה הוא עושה |
|---|---|
| `src/Library.Application/Interfaces/IPasswordHasher.cs` | ממשק ל-hash של סיסמה. בזכותו Application לא תלויה בספריית BCrypt. |
| `src/Library.Application/Interfaces/AccessToken.cs` | ה-token ותאריך התפוגה שלו ב-UTC. |
| `src/Library.Application/Interfaces/IJwtTokenGenerator.cs` | ממשק ליצירת token. המימוש ב-Infrastructure. |
| `src/Library.Application/Interfaces/IAuthService.cs` | החוזה של ההרשמה וההתחברות. |
| `src/Library.Application/Services/AuthService.cs` | נרמול אימייל, בדיקת 72 בתים, הסדר Validation → Validation → Conflict, ו-`Role = Member` תמיד. קריאה אחת ל-SaveChanges, שתופסת `DuplicateValueException` ומחזירה Conflict (כמו ב-CategoryService), כדי לכסות שתי הרשמות במקביל עם אותו אימייל. hash מדומה ב-`s_dummyHash`, עם הערה שהוא מצמצם את פער זמן התגובה אבל לא מבטל אותו. מתודת עזר `CreateResponse`. |
| `src/Library.Infrastructure/Security/BCryptPasswordHasher.cs` | ‏BCrypt עם `WorkFactor = 11`. |
| `src/Library.Infrastructure/Security/JwtTokenGenerator.cs` | בונה את ה-claims, חותם ב-HS256 ומחזיר `AccessToken`. |
| `src/Library.API/Controllers/AuthController.cs` | ‏`POST api/auth/register` ו-`POST api/auth/login` עם `[AllowAnonymous]`. שניהם מחזירים 200, וכישלון עובר דרך `ToProblem`. |

## קבצים ששונו

| קובץ | מה השתנה |
|---|---|
| `src/Library.Application/Common/Error.cs` | נוספו `ErrorType.Unauthorized` ו-`Error.Unauthorized(...)`. |
| `src/Library.Application/DTOs/Auth/AuthResponse.cs` | במקום אובייקט `User` מקונן: `UserId`, `Name`, `Email`, `Role` (UserRole). לא היה ל-AuthResponse מיפוי ב-AutoMapper. |
| `src/Library.Application/DTOs/Auth/RegisterRequest.cs` | הסיסמה מוגבלת ל-`StringLength(72, MinimumLength = 8)`, ונוספה הערה שאין שדה Role בכוונה. |
| `src/Library.Infrastructure/DependencyInjection.cs` | ‏`IAuthService` נרשם כ-scoped. נוסף `AddSecurity()` שרושם את `IPasswordHasher` ואת `IJwtTokenGenerator` כ-singleton, עם הערה למה זה בטוח. |
| `src/Library.API/Program.cs` | ה-Validate של JwtOptions כולל Issuer ו-Audience, סופר בתים ולא תווים, וההודעות בעברית. נוספו `AddSecurity`, `AddAuthentication().AddJwtBearer()`, ‏`JwtBearerOptions` שמוגדרים דרך `IOptions<JwtOptions>`, ‏`AddAuthorization` והגדרת Bearer ב-Swagger. נוסף `UseAuthentication()` לפני `UseAuthorization()`. |
| `src/Library.API/Controllers/ApiControllerBase.cs` | נוספו `[Authorize]`, ‏`[ProducesResponseType(401)]`, ו-`Unauthorized → 401` ב-`ToProblem`. |
| `src/Library.API/Controllers/BooksController.cs` | ‏`[Authorize(Roles = nameof(UserRole.Librarian))]` ו-`[ProducesResponseType(403)]` על Create, Update ו-Delete. |
| `src/Library.API/Controllers/CategoriesController.cs` | אותו דבר על Create, Update ו-Delete. |
| `src/Library.API/Controllers/CopiesController.cs` | אותו דבר על Create ו-Delete. |
| `src/Library.API/Controllers/MembershipPlansController.cs` | ‏`[AllowAnonymous]` על GetAll. |

## הערות

- ה-401 שהוגדר ב-ApiControllerBase עובר בירושה, ולכן Swagger מציג 401 גם ל-register. זה לא מזיק.
- כשה-middleware עצמו מחזיר 401 או 403, הגוף ריק ולא ProblemDetails. רק 401 שעובר דרך `ToProblem` (התחברות שנכשלה) מגיע עם ProblemDetails.

## הסברים

### (א) 401 לעומת 403

- **401** פירושו "לא ידוע מי את": אין token, או שה-token לא תקף (חתימה, issuer, audience או תפוגה).
  - ה-middleware של JwtBearer מחזיר אותו (challenge) כש-`[Authorize]` מ-ApiControllerBase חל על בקשה בלי זהות.
  - בנוסף, `AuthService.LoginAsync` מחזיר `Error.Unauthorized`, ו-`ToProblem` מתרגם אותו ל-401.
- **403** פירושו "ידוע מי את, אבל אין לך הרשאה": ה-token תקף, אבל ה-role הוא Member והפעולה מסומנת `[Authorize(Roles = "Librarian")]`. מערכת ה-Authorization מחזירה אותו (forbid). קיים גם `ErrorType.Forbidden → 403` לבדיקות עסקיות בהמשך.

### (ב) למה Role לא מגיע מהבקשה

ההרשמה פתוחה לכל אחד, בלי token. אם `RegisterRequest` היה כולל Role, כל אחד היה יכול לשלוח `"Role": "Librarian"` ולקבל הרשאות לניהול הקטלוג. לכן ה-role נקבע בשרת (`UserRole.Member` ב-AuthService). אין שדה Role ב-DTO, כך שגם model binding לא יכול למלא אותו.

### (ג) למה UseAuthentication לפני UseAuthorization

- `UseAuthentication` קורא את ה-header ‏`Authorization: Bearer`, מאמת את ה-token וממלא את `HttpContext.User` ב-claims.
- `UseAuthorization` בודק את `[Authorize]` ואת ה-Roles מול `HttpContext.User`.
- בסדר הפוך הבדיקה רצה מול משתמש אנונימי ריק, וכל בקשה מוגנת מקבלת 401 גם עם token תקין.

### (ד) הדרך של NameIdentifier ל-`User.FindFirstValue` בשלב 11

1. `JwtTokenGenerator` מכניס `ClaimTypes.NameIdentifier = user.Id` ל-token. ‏JwtSecurityTokenHandler כותב אותו ב-JSON בשם המקוצר `nameid`.
2. בבקשה הבאה JwtBearer מאמת את ה-token. ה-handler (עם מיפוי ה-claims שמופעל כברירת מחדל) מתרגם את `nameid` בחזרה ל-`ClaimTypes.NameIdentifier` ובונה ממנו `ClaimsPrincipal` ב-`HttpContext.User`, שהוא ה-`User` שזמין ב-controller.
3. ב-controller: `int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture)`.

אותו מיפוי הוא הסיבה ש-`RoleClaimType = ClaimTypes.Role` עובד עם `[Authorize(Roles = ...)]`.

## משתמשי דמו

לכולם אותה סיסמה: **`Demo123!`**

| שם | אימייל | תפקיד | מנוי |
|---|---|---|---|
| שרה לוי | `librarian@library.local` | Librarian | ללא |
| רחל כהן | `member1@library.local` | Member | Basic |
| לאה פרידמן | `member2@library.local` | Member | Standard |

## לפני ההרצה

צריך להגדיר `Jwt:Key` ב-user-secrets, באורך 32 בתים לפחות. בלעדיו `ValidateOnStart` עוצר את עליית האפליקציה:

```
dotnet user-secrets set "Jwt:Key" "<מחרוזת אקראית ארוכה>" --project src/Library.API
```
