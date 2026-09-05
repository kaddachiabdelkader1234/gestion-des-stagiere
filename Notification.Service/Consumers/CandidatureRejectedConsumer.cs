using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Service.Data;
using NotificationEntity = Notification.Service.Models.Notification;
using Notification.Service.Models;
using Notification.Service.Services;
using Stagiaire.Contracts.Events;

namespace Notification.Service.Consumers;

public class CandidatureRejectedConsumer : IConsumer<CandidatureRejected>
{
    private readonly IEmailService _emailService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CandidatureRejectedConsumer> _logger;

    public CandidatureRejectedConsumer(IEmailService emailService, AppDbContext dbContext, ILogger<CandidatureRejectedConsumer> logger)
    {
        _emailService = emailService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CandidatureRejected> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Processing CandidatureRejected for {Nom} {Prenom} ({Email})",
            message.Nom, message.Prenom, message.Email);

        var subject = "Votre candidature n'a pas été retenue — STB";
        var body = $"""
            Bonjour {message.Prenom} {message.Nom},

            Nous avons le regret de vous informer que votre candidature de stage
            n'a pas été retenue.

            Motif : {message.MotifRejet}

            Nous vous invitons à postuler à nouveau pour une prochaine session.

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
                Type = NotificationType.CandidatureRejetee,
                Message = $"Votre candidature de stage n'a pas été retenue. Motif : {message.MotifRejet}",
                Lu = false,
                DateCreation = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Notification record created for user {UserId}", userId);
        }
    }
}
