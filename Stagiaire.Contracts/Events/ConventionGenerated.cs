namespace Stagiaire.Contracts.Events;

public record ConventionGenerated(
    Guid ConventionId,
    Guid StagiaireId,
    string StagiaireNom,
    string StagiairePrenom,
    string StagiaireEmail,
    long? UtilisateurId,
    DateOnly DateGeneration,
    StatutSignature StatutSignature,
    string CheminPdf,
    string? TraceId = null
);

public enum StatutSignature
{
    EnAttente,
    Signee,
    Refusee
}