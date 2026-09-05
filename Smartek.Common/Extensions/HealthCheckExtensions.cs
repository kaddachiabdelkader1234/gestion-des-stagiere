using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Smartek.Common.Extensions;

/// <summary>
/// Health endpoints for Prometheus/Docker/Kubernetes probes.
///
/// Two endpoints, because they answer different questions:
/// <list type="bullet">
///   <item><c>/health/live</c> — is the process up? No dependencies checked, so a database outage
///   does not make an orchestrator kill an otherwise healthy container.</item>
///   <item><c>/health/ready</c> — can it serve traffic? Includes the database.</item>
/// </list>
/// <c>/health</c> is kept as an alias of the readiness check.
/// </summary>
public static class HealthCheckExtensions
{
    public static IServiceCollection AddSmartekHealthChecks<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHealthChecks()
            .AddDbContextCheck<TContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" });

        return services;
    }

    public static IApplicationBuilder UseSmartekHealthChecks(this IApplicationBuilder app)
    {
        var options = new HealthCheckOptions { ResponseWriter = WriteResponse };

        app.UseHealthChecks("/health", options);
        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteResponse
        });

        // Liveness runs no checks at all — Predicate false means "report the process only".
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse
        });

        return app;
    }

    private static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                // Deliberately not entry.Value.Exception — a health endpoint is typically
                // unauthenticated, so it must not expose connection strings or stack traces.
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
