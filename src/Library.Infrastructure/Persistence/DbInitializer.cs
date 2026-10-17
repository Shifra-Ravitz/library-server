using Library.Domain.Entities;
using Library.Domain.Enums;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Runs at startup: applies pending migrations, then creates the demo users.
/// Users are not seeded with HasData because their password hash is generated at runtime.
/// </summary>
public sealed class DbInitializer(AppDbContext db, ILogger<DbInitializer> logger)
{
    // סיסמת דמו ציבורית בכוונה: תופיע ב-README. זה לא סוד.
    private const string DemoPassword = "Demo123!";

    private static readonly (string Name, string Email, UserRole Role, int? PlanId)[] DemoUsers =
    [
        ("שרה לוי",    "librarian@library.local", UserRole.Librarian, null),
        ("רחל כהן",    "member1@library.local",   UserRole.Member,    SeedData.PlanBasicId),
        ("לאה פרידמן", "member2@library.local",   UserRole.Member,    SeedData.PlanStandardId),
    ];

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Applying pending database migrations");
        await db.Database.MigrateAsync(ct);
        await SeedDemoUsersAsync(ct);
    }

    private async Task SeedDemoUsersAsync(CancellationToken ct)
    {
        var emails = DemoUsers.Select(u => u.Email).ToList();
        var existing = await db.Users
            .Where(u => emails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(ct);

        // TODO (stage 10): replace the direct BCrypt call with IPasswordHasher.
        var missing = DemoUsers
            .Where(u => !existing.Contains(u.Email))
            .Select(u => new User
            {
                Name = u.Name,
                Email = u.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
                Role = u.Role,
                MembershipPlanId = u.PlanId,
            })
            .ToList();

        if (missing.Count == 0) return;

        db.Users.AddRange(missing);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} demo users", missing.Count);
    }
}
