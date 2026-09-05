using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Smartek.Common.Errors;

namespace Smartek.Common.Middleware;

/// <summary>
/// Terminal error handler: converts any unhandled exception into an <see cref="ApiErrorResponse"/>.
///
/// Two guarantees, both required by the brief:
/// <list type="bullet">
///   <item>every failure response has the same JSON shape, whatever threw it;</item>
///   <item>no exception message, type, or stack trace ever reaches the client for a 500 — the
///   detail goes to the log, and the client gets a generic message plus a traceId to quote.</item>
/// </list>
/// Register first in the pipeline so it wraps everything after it.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        // Headers are already on the wire — the best we can do is log and let the connection drop,
        // since rewriting the status code now would throw a second time.
        if (context.Response.HasStarted)
        {
            _logger.LogError(exception, "Exception after the response had started; cannot write an error body.");
            throw exception;
        }

        var traceId = context.TraceIdentifier;
        var response = BuildResponse(exception, traceId);

        if (response.Code == ErrorCodes.InternalError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            // Expected, caller-caused outcomes: useful at Information, not worth an error alert.
            _logger.LogInformation(
                "Request rejected: {Code} — {Message} TraceId={TraceId}",
                response.Code, exception.Message, traceId);
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodeFor(exception);
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    private static int StatusCodeFor(Exception exception) => exception switch
    {
        ApiException apiException => apiException.StatusCode,
        OperationCanceledException => 499, // client disconnected; nginx's convention
        _ => StatusCodes.Status500InternalServerError
    };

    private static ApiErrorResponse BuildResponse(Exception exception, string traceId) => exception switch
    {
        ApiException apiException => new ApiErrorResponse
        {
            Error = apiException.Message,
            Code = apiException.Code,
            Errors = apiException.Errors,
            TraceId = traceId
        },

        OperationCanceledException => ApiErrorResponse.Create(
            "La requête a été annulée.", ErrorCodes.BadRequest, traceId),

        // Anything unrecognised is a bug, not a caller error: say nothing specific.
        _ => ApiErrorResponse.Create(
            "Une erreur interne est survenue. Contactez l'administrateur en citant le traceId.",
            ErrorCodes.InternalError,
            traceId)
    };
}
