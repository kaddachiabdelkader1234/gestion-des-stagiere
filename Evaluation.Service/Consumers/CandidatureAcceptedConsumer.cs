using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stagiaire.Contracts.Events;
using Evaluation.Service.Data;
using Evaluation.Service.Models;

namespace Evaluation.Service.Consumers;

/// <summary>
/// Maintains the <see cref="StagiaireAffectation"/> projection so evaluations can be authorised
/// without calling back into Stagiaire.Service.
/// </summary>
/// <remarks>
/// Upserts rather than insert-only: unlike Convention.Service, which creates one draft per acceptance
/// and ignores redeliveries, this row is a *current-state* projection. If an admin re-accepts or the
/// assignment changes, the newest event must win — otherwise the encadrant who may evaluate would be
/// frozen at whoever was assigned first.
/// </remarks>
public class CandidatureAcceptedConsumer(AppDbContext dbContext, ILogger<CandidatureAcceptedConsumer> logger)
    : IConsumer<CandidatureAccepted>
{
    public async Task Consume(ConsumeContext<CandidatureAccepted> context)
    {
        var message = context.Message;

        var affectation = await dbContext.StagiaireAffectations
            .SingleOrDefaultAsync(x => x.StagiaireId == message.StagiaireId, context.CancellationToken);

        if (affectation is null)
        {
            affectation = new StagiaireAffectation { StagiaireId = message.StagiaireId };
            dbContext.StagiaireAffectations.Add(affectation);
        }

        affectation.UtilisateurId = message.UtilisateurId;
        affectation.EncadrantId = message.EncadrantId;
        affectation.Nom = message.Nom.Trim();
        affectation.Prenom = message.Prenom.Trim();
        affectation.Email = message.Email.Trim();
        affectation.DateMiseAJour = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Affectation projected for stagiaire {StagiaireId}: utilisateur {UtilisateurId}, encadrant {EncadrantId}",
            message.StagiaireId, message.UtilisateurId, message.EncadrantId);
    }
}
