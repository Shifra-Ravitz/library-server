namespace Library.Application.DTOs.Loans;

public sealed class LoanResponse
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public string? UserName { get; init; }

    public int BookCopyId { get; init; }

    public int BookId { get; init; }

    public string? BookTitle { get; init; }

    public DateTime LoanedAtUtc { get; init; }

    public DateTime DueAtUtc { get; init; }

    /// <summary>
    /// Null while the loan is still active.
    /// </summary>
    public DateTime? ReturnedAtUtc { get; init; }
}
