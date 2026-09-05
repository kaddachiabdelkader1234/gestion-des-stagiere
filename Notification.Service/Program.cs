using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Notification.Service.Authentication;
using Microsoft.EntityFrameworkCore;
using Steeltoe.Discovery.Eureka;
using Smartek.Common.Extensions;
using Notification.Service.Data;
using Notification.Service.Validation;
using MassTransit;
using Stagiaire.Contracts.Events;
using Notification.Service.Consumers;
using Notification.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSmartekApiConventions();
builder.Services.AddSmartekSwagger(
    "Notification Service API",
    "Consomme les événements RabbitMQ et envoie les notifications par email.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddSmartekHealthChecks<AppDbContext>();

builder.Services.AddEurekaDiscoveryClient();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<NotificationCreateDtoValidator>();

// Email service — configured via Smtp__* env vars (Mailhog in local dev, real SMTP in prod).
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<ConventionGeneratedConsumer>();
    x.AddConsumer<CandidatureAcceptedConsumer>();
    x.AddConsumer<EvaluationSubmittedConsumer>();
    x.AddConsumer<CandidatureRejectedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Configure message endpoints
        cfg.ReceiveEndpoint("convention-generated-queue", e =>
        {
            e.ConfigureConsumer<ConventionGeneratedConsumer>(context);
        });

        cfg.ReceiveEndpoint("candidature-accepted-queue", e =>
        {
            e.ConfigureConsumer<CandidatureAcceptedConsumer>(context);
        });

        cfg.ReceiveEndpoint("evaluation-submitted-queue", e =>
        {
            e.ConfigureConsumer<EvaluationSubmittedConsumer>(context);
        });

        cfg.ReceiveEndpoint("candidature-rejected-queue", e =>
        {
            e.ConfigureConsumer<CandidatureRejectedConsumer>(context);
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

app.UseSmartekSwagger("Notification Service API");

// See Stagiaire.Service/Program.cs — TLS terminates at the gateway, so no HTTPS redirect here.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSmartekFallback();

app.Run();

public partial class Program;