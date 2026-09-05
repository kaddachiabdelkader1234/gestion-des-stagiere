using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stagiaire.Contracts.Events;
using Convention.Service.Data;
using Convention.Service.Models;
// The entity type `Convention` is shadowed by the `Convention.Service` root namespace inside this
// namespace, so alias it — same pattern Stagiaire.Service uses for `Stagiaire`.
using ConventionEntity = Convention.Service.Models.Convention;
using StatutSignatureEntity = Convention.Service.Models.StatutSignature;

namespace Convention.Service.Consumers;

/// <summary>
/// Auto-creates a draft convention when a candidature is accepted.
///
/// The stagiaire details (name, email, dates) that the PDF needs are persisted here from the event,
/// so PDF generation never has to call back into Stagiaire.Service.
/// </summary>
public class CandidatureAcceptedConsumer(AppDbContext dbContext, ILogger<CandidatureAcceptedConsumer> logger)
    : IConsumer<CandidatureAccepted>
{
    public async Task Consume(ConsumeContext<CandidatureAccepted> context)
    {
        var message = context.Message;

        // Propagate the trace ID from the originating HTTP request into this service's logs.
        using var scope = !string.IsNullOrEmpty(message.TraceId)
            ? logger.BeginScope(new Dictionary<string, object?> { ["TraceId"] = message.TraceId })
            : null;

        // Idempotence: an event redelivery (MassTransit retries) must not create a second row.
        var exists = await dbContext.Conventions
            .AsNoTracking()
            .AnyAsync(x => x.StagiaireId == message.StagiaireId, context.CancellationToken);

        if (exists)
        {
            logger.LogInformation(
                "Convention already exists for stagiaire {StagiaireId}; ignoring redelivered event",
                message.StagiaireId);
            return;
        }

        var convention = new ConventionEntity
        {
            Id = Guid.NewGuid(),
            StagiaireId = message.StagiaireId,
            // Copied so the convention can be scoped by role without calling back into
            // Stagiaire.Service on every read.
            UtilisateurId = message.UtilisateurId,
            EncadrantId = message.EncadrantId,
            StagiaireNom = message.Nom.Trim(),
            StagiairePrenom = message.Prenom.Trim(),
            StagiaireEmail = message.Email.Trim(),
            Departement = message.Departement.Trim(),
            DateDebut = message.DateDebut,
            DateFin = message.DateFin,
            DateGeneration = DateOnly.FromDateTime(DateTime.UtcNow),
            StatutSignature = StatutSignatureEntity.EnAttente,
            CheminPdf = string.Empty
        };

        dbContext.Conventions.Add(convention);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Draft convention {ConventionId} auto-created for accepted candidature {StagiaireId}",
            convention.Id, message.StagiaireId);
    }
}