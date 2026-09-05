namespace Stagiaire.Contracts.Events;

/// <summary>
/// Published by Stagiaire.Service when an admin accepts a candidature.
/// </summary>
/// <param name="StagiaireId">Guid of the stagiaire record — the correlation key consumers key on.</param>
/// <param name="UtilisateurId">
/// auth-service <c>users.user_id</c> of the learner who owns the candidature, or null when an admin
/// created the record for someone with no account yet.
///
/// Carried so Convention.Service can enforce "a learner only ever sees their own convention" from
/// the JWT. A convention row otherwise holds only <c>StagiaireId</c>, which cannot be matched
/// against a caller's identity without calling back into this service on every request.
/// </param>
/// <param name="EncadrantId">
/// auth-service <c>users.user_id</c> of the assigned TRAINER, for the equivalent
/// "an encadrant only sees their own assigned stagiaires" boundary.
/// </param>
public record CandidatureAccepted(
    Guid StagiaireId,
    string Nom,
    string Prenom,
    string Email,
    string Departement,
    DateOnly DateDebut,
    DateOnly DateFin,
    long? UtilisateurId,
    long? EncadrantId,
    string? TraceId = null
);
