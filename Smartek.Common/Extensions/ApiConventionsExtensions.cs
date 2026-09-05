using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Smartek.Common.Errors;
using Smartek.Common.Middleware;
using System.Text.Json;

namespace Smartek.Common.Extensions;

/// <summary>
/// Shared API setup so all four services present the same error shape, the same validation
/// format, and comparable Swagger docs.
/// </summary>
public static class ApiConventionsExtensions
{
    /// <summary>
    /// Replaces MVC's default 400 body with <see cref="ApiErrorResponse"/>.
    ///
    /// Without this, a model-binding or FluentValidation failure returns ASP.NET's
    /// ValidationProblemDetails (`{ type, title, status, errors }`) while a handler-thrown error
    /// returns our shape — two different contracts for the same class of failure.
    /// </summary>
    public static IServiceCollection AddSmartekApiConventions(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => ToCamelCase(entry.Key),
                        entry => entry.Value!.Errors
                            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                // A binding failure (e.g. "abc" for a Guid) has no message, only
                                // an exception we must not surface.
                                ? "La valeur fournie est invalide."
                                : error.ErrorMessage)
                            .ToArray());

                var response = new ApiErrorResponse
                {
                    Error = "Un ou plusieurs champs sont invalides.",
                    Code = ErrorCodes.ValidationError,
                    Errors = errors,
                    TraceId = context.HttpContext.TraceIdentifier
                };

                return new BadRequestObjectResult(response)
                {
                    ContentTypes = { "application/json" }
                };
            };
        });

        return services;
    }

    /// <summary>
    /// Swagger with a Bearer scheme, so /swagger can exercise authenticated endpoints directly.
    /// </summary>
    /// <param name="title">Display name, e.g. "Stagiaire Service API".</param>
    /// <param name="description">One-line summary of the service's responsibility.</param>
    public static IServiceCollection AddSmartekSwagger(
        this IServiceCollection services,
        string title,
        string description)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = title,
                Version = "v1",
                Description = $"{description}\n\n" +
                              "Toutes les requêtes passent normalement par l'API Gateway sur " +
                              "http://localhost:18080 (routes /api/v1/**). Les erreurs suivent le " +
                              "format { \"error\": \"...\", \"code\": \"...\" }."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT HS256 émis par auth-service. Collez le token seul, sans le préfixe \"Bearer\"."
            };

            options.AddSecurityDefinition("Bearer", scheme);

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });

            // Surfaces DateOnly as "2026-09-01" instead of an object with Year/Month/Day members.
            options.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
            options.MapType<DateOnly?>(() => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });
        });

        return services;
    }

    /// <summary>
    /// Installs the exception handler. Call before any middleware whose failures should be
    /// reported as JSON.
    /// </summary>
    public static IApplicationBuilder UseSmartekExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();

    /// <summary>
    /// Serves Swagger UI at <c>/swagger</c> in every environment.
    /// </summary>
    /// <remarks>
    /// The brief asks for Swagger exposed per service; gating it behind IsDevelopment (the previous
    /// behaviour) hides it in the Docker Compose stack whenever ASPNETCORE_ENVIRONMENT is not
    /// Development. It carries no secrets — the schema is already implied by the public routes.
    /// </remarks>
    public static IApplicationBuilder UseSmartekSwagger(this IApplicationBuilder app, string title)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{title} v1");
            options.DocumentTitle = title;
        });

        return app;
    }

    /// <summary>
    /// Answers unmatched routes with the shared error shape.
    /// </summary>
    /// <remarks>
    /// Call after MapControllers. Without it, a URL that fails a route constraint — say
    /// <c>/api/v1/stagiaires/not-a-guid</c> against <c>{id:guid}</c> — matches no endpoint and
    /// ASP.NET returns a bodiless 404, the one failure that did not carry the documented
    /// <c>{ error, code }</c> body.
    /// </remarks>
    public static IEndpointRouteBuilder MapSmartekFallback(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFallback(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = ApiErrorResponse.Create(
                "La ressource demandée est introuvable. Vérifiez l'URL et le format des identifiants.",
                ErrorCodes.NotFound,
                context.TraceIdentifier);

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        });

        return endpoints;
    }

    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrEmpty(key) || char.IsLower(key[0]))
        {
            return key;
        }

        // ModelState keys are PascalCase property paths ("DateFin", "Adresse.Ville"); the client
        // sees camelCase JSON, so field names must match what it sent.
        return string.Join('.', key.Split('.').Select(LowercaseFirst));
    }

    private static string LowercaseFirst(string segment) =>
        string.IsNullOrEmpty(segment) || char.IsLower(segment[0])
            ? segment
            : char.ToLowerInvariant(segment[0]) + segment[1..];
}
