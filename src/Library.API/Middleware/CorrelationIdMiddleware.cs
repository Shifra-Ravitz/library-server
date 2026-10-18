using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Library.API.Middleware;

/// <summary>
/// Gives every request a correlation id: the X-Correlation-ID request header when it is safe to echo back,
/// otherwise a new Guid. The id is stored in HttpContext.Items, returned in the response header,
/// and added to the logging scope of everything logged while the request runs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // A repeated header is not accepted: its values would be joined with ',' anyway.
        var incoming = context.Request.Headers.TryGetValue(HeaderName, out var values) && values.Count == 1
            ? values[0]
            : null;
        var correlationId = IsValidCorrelationId(incoming) ? incoming : Guid.NewGuid().ToString();

        context.Items[ItemKey] = correlationId;

        // Set, not appended, so it is written once even if ExceptionHandlingMiddleware already set it.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            // Path only: the query string and the body may hold personal data.
            var method = context.Request.Method;
            var path = context.Request.Path;

            _logger.LogInformation("HTTP {Method} {Path} started", method, path);
            var startTimestamp = Stopwatch.GetTimestamp();

            try
            {
                await _next(context);
            }
            catch
            {
                // No status here: ExceptionHandlingMiddleware decides it, and it also logs the exception itself.
                _logger.LogInformation("HTTP {Method} {Path} failed after {ElapsedMs:0} ms",
                    method, path, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
                throw;
            }

            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms",
                method, path, context.Response.StatusCode, elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// The value is echoed back in a header and written to logs, so only a short, plain token is accepted.
    /// </summary>
    private static bool IsValidCorrelationId([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
