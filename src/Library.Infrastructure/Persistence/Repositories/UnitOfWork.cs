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
        // Only a unique-index violation is translated, so Application never sees EF or Npgsql types.
        // Every other exception (including concurrency) propagates unchanged.
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException
            && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new DuplicateValueException(pg.ConstraintName, ex);
        }
    }
}
