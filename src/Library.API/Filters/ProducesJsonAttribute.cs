using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Library.API.Filters;

/// <summary>
/// [Produces("application/json")] that leaves ProblemDetails results alone. The plain attribute replaces the content
/// types of every ObjectResult, so Problem() and the automatic 400 of [ApiController] would be sent as application/json
/// instead of application/problem+json. Swagger still documents application/json for every response.
/// </summary>
public sealed class ProducesJsonAttribute : ProducesAttribute
{
    public ProducesJsonAttribute()
        : base("application/json")
    {
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        // ValidationProblemDetails derives from ProblemDetails, so the automatic 400 is covered too.
        if (context.Result is ObjectResult { Value: ProblemDetails })
        {
            return;
        }

        base.OnResultExecuting(context);
    }
}
