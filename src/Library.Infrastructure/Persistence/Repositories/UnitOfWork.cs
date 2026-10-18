using Library.Application.Common.Exceptions;
using Library.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    // Goes through the AppDbContext override, which regenerates BookCopy.Version.
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        // An UPDATE matched 0 rows because the Version in its WHERE was already replaced by another request.
        // The whole save was rolled back. This is the only catch of DbUpdateConcurrencyException in the solution.
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The data was changed by another request after it was read. No changes were saved.", ex);
        }
        // Only a unique-index violation is translated, so Application never sees EF or Npgsql types.
        // Every other exception propagates unchanged.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new DuplicateValueException(pg.ConstraintName, ex);
        }
    }
}
