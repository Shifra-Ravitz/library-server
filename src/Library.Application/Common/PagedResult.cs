namespace Library.Application.Common;

/// <summary>
/// One page of results returned by the services, plus the information the client needs to page further.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>
    /// Number of pages; 0 when <see cref="PageSize"/> is not positive (avoids division by zero).
    /// </summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
