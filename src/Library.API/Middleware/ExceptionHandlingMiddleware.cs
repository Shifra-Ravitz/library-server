using Microsoft.AspNetCore.Mvc;

namespace Library.API.Middleware;

/// <summary>
/// The outermost middleware. Turns an exception that escaped the pipeline into a ProblemDetails response
/// with only a fixed title and the correlation id; the exception itself is written to the log only.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string BadRequestTitle = "הבקשה אינה תקינה.";
    private const string ServerErrorTitle = "אירעה שגיאה בלתי צפויה בשרת.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IProblemDetailsService problemDetails)
    {
        _next = next;
        _logger = logger;
        _problemDetails = problemDetails;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client is gone, so nobody reads a body. Not an error of the server.
            using (BeginCorrelationScope(context))
            {
                _logger.LogInformation("HTTP {Method} {Path} was cancelled by the client",
                    context.Request.Method, context.Request.Path);
            }

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
        }
        catch (BadHttpRequestException ex)
        {
            // Thrown by Kestrel and model binding for a malformed request (for example a body that is too large).
            using (BeginCorrelationScope(context))
            {
                _logger.LogWarning("Bad HTTP request {Method} {Path} -> {StatusCode}",
                    context.Request.Method, context.Request.Path, ex.StatusCode);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context, ex.StatusCode, BadRequestTitle);
        }
        catch (Exception ex)
        {
            using (BeginCorrelationScope(context))
            {
                _logger.LogError(ex, "Unhandled exception for HTTP {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }

            // Headers and part of the body were already sent; a problem response cannot be written any more.
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, ServerErrorTitle);
        }
    }

    /// <summary>
    /// The scope opened by CorrelationIdMiddleware is already closed when the exception reaches this point,
    /// so it is opened again here from HttpContext.Items.
    /// </summary>
    private IDisposable? BeginCorrelationScope(HttpContext context)
    {
        var correlationId = context.GetCorrelationId();
        return correlationId is null
            ? null
            : _logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId });
    }

    private async Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        var response = context.Response;

        // Drops headers and status that the failed request had already set (for example a Location or a Content-Type).
        response.Clear();
        response.StatusCode = statusCode;

        var problem = new ProblemDetails { Status = statusCode, Title = title };

        var correlationId = context.GetCorrelationId();
        if (correlationId is not null)
        {
            // Clear() removed all headers, so the id is set again here instead of relying on OnStarting alone.
            response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
            problem.Extensions["correlationId"] = correlationId;
        }

        var written = await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });

        // TryWriteAsync returns false when no writer accepts the request's Accept header (for example text/html).
        if (!written)
        {
            await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
