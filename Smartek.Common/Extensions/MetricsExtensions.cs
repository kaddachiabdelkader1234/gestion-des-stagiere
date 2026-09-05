using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;

namespace Smartek.Common.Extensions;

/// <summary>
/// Wires up Prometheus metrics collection for a .NET service.
/// Adds request count, latency histogram, and in-flight gauge.
/// The /metrics endpoint is served by prometheus-net's middleware.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Adds Prometheus metrics middleware and the /metrics endpoint.
    /// Call after UseRouting() and before MapControllers().
    /// </summary>
    public static IApplicationBuilder UseSmartekMetrics(this IApplicationBuilder app)
    {
        // Expose /metrics endpoint for Prometheus scraping
        app.UseMetricServer();

        // Collect HTTP request metrics (count, duration, in-flight)
        app.UseHttpMetrics();

        return app;
    }

    /// <summary>
    /// Registers Prometheus services (counter, histogram, etc.) in the DI container.
    /// </summary>
    public static IServiceCollection AddSmartekMetrics(this IServiceCollection services)
    {
        // Prometheusetheus-net registers its own services internally.
        // No additional registration needed beyond what UseMetricServer/UseHttpMetrics provide.
        return services;
    }
}
