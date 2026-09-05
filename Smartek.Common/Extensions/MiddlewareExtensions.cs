using Microsoft.AspNetCore.Builder;
using Smartek.Common.Middleware;

namespace Smartek.Common.Extensions;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="TraceIdMiddleware"/> that reads the <c>X-Trace-Id</c> header
    /// from the gateway and sets it as the request's trace identifier.
    /// </summary>
    public static IApplicationBuilder UseSmartekTracing(this IApplicationBuilder app)
    {
        app.UseMiddleware<TraceIdMiddleware>();
        return app;
    }
}
