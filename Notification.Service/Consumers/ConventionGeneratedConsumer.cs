using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Service.Data;
using NotificationEntity = Notification.Service.Models.Notification;
using Notification.Service.Models;
using Notification.Service.Services;
using Stagiaire.Contracts.Events;

namespace Notification.Service.Consumers;

public class ConventionGeneratedConsumer : IConsumer<ConventionGenerated>
{
    private readonly IEmailService _emailService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ConventionGeneratedConsumer> _logger;

    public ConventionGeneratedConsumer(IEmailService emailService, AppDbContext dbContext, ILogger<ConventionGeneratedConsumer> logger)
    {
        _emailService = emailService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ConventionGenerated> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Processing ConventionGenerated for {Nom} {Prenom} ({Email})",
            message.StagiaireNom, message.StagiairePrenom, message.StagiaireEmail);

        var subject = "Votre convention de stage est prête — STB";
        var body = $"""
            Bonjour {message.StagiairePrenom} {message.StagiaireNom},

            Votre convention de stage a été générée le {message.DateGeneration:dd/MM/yyyy}.

            Statut : {FormatStatut(message.StatutSignature)}

            Vous pouvez télécharger le PDF depuis votre espace personnel.

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
                Type = NotificationType.ConventionGeneree,
                Message = $"Votre convention de stage a été générée. Téléchargez-la depuis votre espace personnel.",
                Lu = false,
                DateCreation = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Notification record created for user {UserId}", userId);
        }
    }

    private static string FormatStatut(StatutSignature statut) => statut switch
    {
        StatutSignature.EnAttente => "En attente de signature",
        StatutSignature.Signee => "Signée",
        StatutSignature.Refusee => "Refusée",
        _ => statut.ToString()
    };
}
