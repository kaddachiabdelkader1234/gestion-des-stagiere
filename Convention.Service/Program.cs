using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Convention.Service.Authentication;
using Microsoft.EntityFrameworkCore;
using Steeltoe.Discovery.Eureka;
using Smartek.Common.Extensions;
using Smartek.Common.Storage;
using Convention.Service.Consumers;
using Convention.Service.Data;
using Convention.Service.Pdf;
using Convention.Service.Services;
using Convention.Service.Validation;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSmartekApiConventions();
builder.Services.AddSmartekSwagger(
    "Convention Service API",
    "Génération et signature des conventions de stage (PDF).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddSmartekHealthChecks<AppDbContext>();

// Generated convention PDFs land on a local volume (Storage:RootPath), not in the database.
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IConventionPdfGenerator, ConventionPdfGenerator>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddEurekaDiscoveryClient();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ConventionCreateDtoValidator>();

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Auto-creates a draft convention when a candidature is accepted.
    x.AddConsumer<CandidatureAcceptedConsumer>();

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

app.UseSmartekTracing();
app.UseSmartekExceptionHandling();

// Prometheus metrics — /metrics endpoint for scraping, HTTP request counters/histograms.
app.UseSmartekMetrics();

app.UseSmartekHealthChecks();
app.UseSmartekSwagger("Convention Service API");

// See Stagiaire.Service/Program.cs — TLS terminates at the gateway, so no HTTPS redirect here.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSmartekFallback();

app.Run();

public partial class Program;
