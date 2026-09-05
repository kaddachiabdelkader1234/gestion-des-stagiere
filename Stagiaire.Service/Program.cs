using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Stagiaire.Service.Authentication;
using Microsoft.EntityFrameworkCore;
using Steeltoe.Discovery.Eureka;
using Smartek.Common.Extensions;
using Smartek.Common.Storage;
using Stagiaire.Service.Data;
using Stagiaire.Service.Validation;
using MassTransit;
using Stagiaire.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Validation failures answer with the same { error, code, errors } shape as everything else.
builder.Services.AddSmartekApiConventions();
builder.Services.AddSmartekSwagger(
    "Stagiaire Service API",
    "Gestion des stagiaires et des candidatures de stage.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddSmartekHealthChecks<AppDbContext>();

// CV uploads land on a local volume (Storage:RootPath), not in the database.
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddEurekaDiscoveryClient();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<StagiaireCreateDtoValidator>();

// Audit trail — every state-changing action writes an entry to AuditEntries.
builder.Services.AddScoped<IAuditService, AuditService>();

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Creates the database if absent and applies pending migrations before serving traffic.
await app.MigrateDatabaseAsync<AppDbContext>();

// First in the pipeline: converts any unhandled exception into the shared error shape so no
// stack trace can reach the client.
app.UseSmartekTracing();
app.UseSmartekExceptionHandling();

// Prometheus metrics — /metrics endpoint for scraping, HTTP request counters/histograms.
app.UseSmartekMetrics();

app.UseSmartekHealthChecks();
app.UseSmartekSwagger("Stagiaire Service API");

// No UseHttpsRedirection: the container listens on plain HTTP (ASPNETCORE_URLS=http://+:8080)
// and TLS terminates at the gateway. With no HTTPS port to resolve, the middleware only logged
// "Failed to determine the https port for redirect" on every request and passed it through.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSmartekFallback();

app.Run();

public partial class Program;