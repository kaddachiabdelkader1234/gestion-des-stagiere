using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Service.Data;
using NotificationEntity = Notification.Service.Models.Notification;
using Notification.Service.Models;
using Notification.Service.Services;
using Stagiaire.Contracts.Events;

namespace Notification.Service.Consumers;

public class EvaluationSubmittedConsumer : IConsumer<EvaluationSubmitted>
{
    private readonly IEmailService _emailService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EvaluationSubmittedConsumer> _logger;

    public EvaluationSubmittedConsumer(IEmailService emailService, AppDbContext dbContext, ILogger<EvaluationSubmittedConsumer> logger)
    {
        _emailService = emailService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EvaluationSubmitted> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Processing EvaluationSubmitted for {Nom} {Prenom} — {Type} note {Note}/20",
            message.StagiaireNom, message.StagiairePrenom, message.TypeEvaluation, message.Note);

        if (string.IsNullOrWhiteSpace(message.StagiaireEmail))
        {
            _logger.LogWarning(
                "No email address for stagiaire {StagiaireId} — skipping notification",
                message.StagiaireId);
            return;
        }

        var typeLabel = message.TypeEvaluation switch
        {
            TypeEvaluation.MiParcours => "mi-parcours",
            TypeEvaluation.Finale => "finale",
            _ => message.TypeEvaluation.ToString()
        };

        var subject = $"Résultat de votre évaluation {typeLabel} — STB";
        var body = $"""
            Bonjour {message.StagiairePrenom} {message.StagiaireNom},

            Votre évaluation de type {typeLabel} a été enregistrée.

            Note : {message.Note}/20

            {(!string.IsNullOrWhiteSpace(message.Commentaire) ? $"Commentaire :\n{message.Commentaire}\n" : "")}
            Cordialement,
            L'équipe STB
            """;

        await _emailService.SendEmailAsync(message.StagiaireEmail, subject, body, context.CancellationToken);

        // Persist a Notification record so the in-app notifications screen shows it.
        if (message.UtilisateurId is { } userId)
        {
            var notification = new NotificationEntity
            {
                Id = Guid.NewGuid(),
                DestinataireId = userId,
                DestinataireRole = DestinataireRole.Stagiaire,
                Type = NotificationType.EvaluationSoumise,
                Message = $"Votre évaluation {typeLabel} a été enregistrée. Note : {message.Note}/20",
                Lu = false,
                DateCreation = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Notification record created for user {UserId}", userId);
        }
    }
}
