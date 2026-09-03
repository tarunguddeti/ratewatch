namespace CurrencyWatchlist.Api.Middleware;

/// <summary>One server-generated trace ID per request (HttpContext.TraceIdentifier, not a
/// hand-rolled or client-issued correlation ID). Propagates through a logging scope so every
/// ILogger call anywhere during this request - controller, service, deep inside
/// FrankfurterRateProvider - automatically carries the same TraceId as a structured field.</summary>
public class TraceIdMiddleware(RequestDelegate next, ILogger<TraceIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        context.Response.Headers["X-Trace-Id"] = traceId;

        using (logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
        {
            await next(context);
        }
    }
}
