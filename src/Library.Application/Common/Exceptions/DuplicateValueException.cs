namespace Library.Application.Common.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IUnitOfWork.SaveChangesAsync"/> when a save violates a unique index,
/// e.g. two concurrent requests that create a category with the same name.
/// </summary>
public sealed class DuplicateValueException : Exception
{
    public DuplicateValueException(string? constraintName, Exception innerException)
        : base($"A unique constraint was violated ({constraintName ?? "unknown"}).", innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Name of the violated unique index, when the database reports it.
    /// </summary>
    public string? ConstraintName { get; }
}
