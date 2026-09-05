using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Service.Data;
using NotificationEntity = Notification.Service.Models.Notification;
using Notification.Service.Models;
using Notification.Service.Services;
using Stagiaire.Contracts.Events;

namespace Notification.Service.Consumers;

public class CandidatureAcceptedConsumer : IConsumer<CandidatureAccepted>
{
    private readonly IEmailService _emailService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CandidatureAcceptedConsumer> _logger;

    public CandidatureAcceptedConsumer(IEmailService emailService, AppDbContext dbContext, ILogger<CandidatureAcceptedConsumer> logger)
    {
        _emailService = emailService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CandidatureAccepted> context)
    {
        var message = context.Message;

        // Propagate the trace ID from the originating HTTP request into this service's logs.
        using var scope = !string.IsNullOrEmpty(message.TraceId)
            ? _logger.BeginScope(new Dictionary<string, object?> { ["TraceId"] = message.TraceId })
            : null;

        _logger.LogInformation(
            "Processing CandidatureAccepted for {Nom} {Prenom} ({Email})",
            message.Nom, message.Prenom, message.Email);

        var subject = "Votre candidature a été acceptée — STB";
        var body = $"""
            Bonjour {message.Prenom} {message.Nom},

            Nous avons le plaisir de vous informer que votre candidature de stage
            au département {message.Departement} a été acceptée.

            Dates du stage : {message.DateDebut:dd/MM/yyyy} au {message.DateFin:dd/MM/yyyy}

            Une convention de stage sera prochainement générée. Vous pourrez la
            télécharger depuis votre espace personnel.

            Cordialement,
            L'équipe STB
            """;

        await _emailService.SendEmailAsync(message.Email, subject, body, context.CancellationToken);

        // Persist a Notification record so the in-app notifications screen shows it.
        if (message.UtilisateurId is { } userId)
        {
            var notification = new NotificationEntity
            {
                Id = Guid.NewGuid(),
                DestinataireId = userId,
                DestinataireRole = DestinataireRole.Stagiaire,
                Type = NotificationType.CandidatureAcceptee,
                Message = $"Votre candidature de stage au département {message.Departement} a été acceptée.",
                Lu = false,
                DateCreation = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Notification record created for user {UserId}", userId);
        }
    }
}
