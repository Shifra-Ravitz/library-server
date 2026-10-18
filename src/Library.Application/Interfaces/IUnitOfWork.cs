namespace Library.Application.Interfaces;

/// <summary>
/// Commits all changes tracked during the current request in a single database transaction.
/// Repositories only stage changes; this is the only place that saves them.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Common.Exceptions.ConcurrencyConflictException">
    /// A row protected by a concurrency token was changed by another request after it was read.
    /// Nothing from this save is committed, and the same context must not be saved again.
    /// </exception>
    /// <exception cref="Common.Exceptions.DuplicateValueException">The save violates a unique index.</exception>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
