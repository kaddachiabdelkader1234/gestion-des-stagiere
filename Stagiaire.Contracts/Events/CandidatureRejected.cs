namespace Stagiaire.Contracts.Events;

/// <summary>
/// Published by Stagiaire.Service when an admin rejects a candidature.
/// </summary>
public record CandidatureRejected(
    Guid StagiaireId,
    string Nom,
    string Prenom,
    string Email,
    string MotifRejet,
    long? UtilisateurId,
    string? TraceId = null
);
