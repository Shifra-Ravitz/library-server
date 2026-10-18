namespace Library.API.Middleware;

public static class CorrelationIdHttpContextExtensions
{
    /// <summary>
    /// The correlation id that CorrelationIdMiddleware stored for this request, or null if it has not run.
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) ? value as string : null;
}
