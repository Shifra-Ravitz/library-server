using Library.Infrastructure.Persistence; // namespace של AppDbContext: לפי הקוד בפועל
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Library.Tests.Integration;

// מרים קונטיינר PostgreSQL אחד לכל מחלקת בדיקות, מריץ migrations, ומוחק אותו בסוף.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // אותם migrations שהשרת מריץ בעלייה, כולל ה-seed של HasData
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    // כל קריאה מחזירה context חדש ונפרד, כמו בקשת HTTP חדשה בשרת
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
