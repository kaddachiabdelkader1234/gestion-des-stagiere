using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Evaluation.Service.Authentication;
using Evaluation.Service.Consumers;
using Microsoft.EntityFrameworkCore;
using Steeltoe.Discovery.Eureka;
using Smartek.Common.Extensions;
using Evaluation.Service.Data;
using Evaluation.Service.Services;
using Evaluation.Service.Validation;
using Evaluation.Service.Pdf;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSmartekApiConventions();
builder.Services.AddSmartekSwagger(
    "Evaluation Service API",
    "Évaluations de fin de stage : grille de compétences et note finale.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddSmartekHealthChecks<AppDbContext>();

builder.Services.AddEurekaDiscoveryClient();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<EvaluationCreateDtoValidator>();

// Attestation PDF generation — same QuestPDF/DejaVu approach as Convention.Service.
builder.Services.AddSingleton<IAttestationPdfGenerator, AttestationPdfGenerator>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Maintains the StagiaireAffectation projection that authorisation depends on.
    x.AddConsumer<CandidatureAcceptedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Explicit queue name, and deliberately NOT cfg.ConfigureEndpoints(context).
        //
        // The default endpoint name is derived from the consumer's type name, and Convention.Service
        // already has a class called CandidatureAcceptedConsumer. With ConfigureEndpoints, both
        // services asked for a queue named "CandidatureAccepted" and became *competing consumers* on
        // it — RabbitMQ then delivered each event to only one of them, so a candidature acceptance
        // either drafted a convention or projected an affectation, never both, at random.
        //
        // Notification.Service names all three of its endpoints explicitly for the same reason. Any
        // second consumer of an event another service already handles must do this.
        cfg.ReceiveEndpoint("evaluation-candidature-accepted-queue", e =>
        {
            e.ConfigureConsumer<CandidatureAcceptedConsumer>(context);
        });
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

app.UseSmartekSwagger("Evaluation Service API");

// See Stagiaire.Service/Program.cs — TLS terminates at the gateway, so no HTTPS redirect here.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSmartekFallback();

app.Run();

public partial class Program;