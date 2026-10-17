namespace Library.Application.Interfaces;

/// <summary>
/// Commits all changes tracked during the current request in a single database transaction.
/// Repositories only stage changes; this is the only place that saves them.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
