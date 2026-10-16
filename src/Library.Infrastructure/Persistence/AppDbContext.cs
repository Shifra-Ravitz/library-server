using Library.Domain.Common;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the library. Entity mappings live in
/// <c>Persistence/Configurations</c> and are applied from this assembly.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void UpdateVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IHasVersion>())
        {
            if (entry.State == EntityState.Modified)
            {
                // Only the current value changes; the old Version stays in OriginalValue,
                // and EF puts it in the UPDATE's WHERE clause. If another request changed
                // the row first, no row matches and DbUpdateConcurrencyException is thrown.
                entry.Entity.Version = Guid.NewGuid();
            }
            else if (entry.State == EntityState.Added && entry.Entity.Version == Guid.Empty)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }
    }
}
