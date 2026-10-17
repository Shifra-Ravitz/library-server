using System.ComponentModel.DataAnnotations;
using Library.Application.Common;

namespace Library.Application.DTOs.Loans;

/// <summary>
/// Query string of GET /api/loans. The service converts it to <see cref="LoanSearchFilter"/>
/// and supplies UserId and NowUtc itself.
/// </summary>
public sealed class LoanQueryParameters
{
    public bool ActiveOnly { get; set; }

    public bool OverdueOnly { get; set; }

    [Range(1, Paging.MaxPage)]
    public int Page { get; set; } = 1;

    [Range(1, Paging.MaxPageSize)]
    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
