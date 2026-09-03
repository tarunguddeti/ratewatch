using CurrencyWatchlist.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyWatchlist.Api.Middleware;

/// <summary>The single shared error-response mechanism at the HTTP boundary. Log level
/// follows whether the failure is expected, not whether it's 4xx/5xx: Warning (no stack
/// trace) for anything the system is designed to hand back to a caller, Error (full
/// exception + trace ID) for provider outages and anything unhandled.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>The single source of truth for this content type - Program.cs's
    /// InvalidModelStateResponseFactory previously restated it independently.</summary>
    public const string ProblemJson = "application/problem+json";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = Map(ex);
            logger.Log(
                status >= 500 ? LogLevel.Error : LogLevel.Warning,
                status >= 500 ? ex : null,
                "{Title} ({Status}) for {Method} {Path}",
                title, status, context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = ex.Message,
            };

            // Only 5xx responses show a trace ID - a 4xx is already fixable from the message
            // alone, and a trace ID there would just be clutter.
            if (status >= 500)
            {
                problem.Extensions["traceId"] = context.TraceIdentifier;
            }

            context.Response.ContentType = ProblemJson;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Title) Map(Exception ex) => ex switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
        DuplicatePairException => (StatusCodes.Status409Conflict, "Duplicate pair"),
        Application.Exceptions.UnsupportedPairException => (StatusCodes.Status422UnprocessableEntity, "Unsupported currency pair"),
        RateProviderUnavailableException => (StatusCodes.Status502BadGateway, "Rate provider unavailable"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
    };
}
