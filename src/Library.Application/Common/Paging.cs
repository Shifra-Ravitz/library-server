namespace Library.Application.Common;

/// <summary>
/// Paging limits shared by the query DTOs (validation) and the services (defaults).
/// </summary>
public static class Paging
{
    public const int DefaultPageSize = 10;

    public const int MaxPageSize = 50;

    /// <summary>
    /// Upper bound for the page number, so (Page - 1) * PageSize can never overflow an int.
    /// </summary>
    public const int MaxPage = 10_000;
}
