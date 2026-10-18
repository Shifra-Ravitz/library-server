using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Loans;

/// <summary>
/// Body of POST /api/loans. The borrower is taken from the JWT, not from the body.
/// </summary>
public sealed class CreateLoanRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "יש לבחור עותק.")]
    public int CopyId { get; set; }
}
