namespace Stagiaire.Contracts.Events;

/// <summary>
/// Published when an encadrant submits an evaluation.
/// </summary>
/// <param name="EncadrantId">
/// auth-service <c>users.user_id</c> of the assigned encadrant — a BIGINT there, so <c>long</c> here.
/// This was a <c>Guid</c>, which could never match the <c>userId</c> claim in a JWT and so made
/// role-scoping on it impossible.
/// </param>
public record EvaluationSubmitted(
    Guid EvaluationId,
    Guid StagiaireId,
    string StagiaireNom,
    string StagiairePrenom,
    string StagiaireEmail,
    long? UtilisateurId,
    long EncadrantId,
    TypeEvaluation TypeEvaluation,
    decimal Note,
    string Commentaire,
    StatutEvaluation Statut,
    string? TraceId = null
);

/// <summary>
/// Mirrors <c>Evaluation.Service.Models.TypeEvaluation</c> — keep the members and their order
/// identical.
/// </summary>
/// <remarks>
/// These were <c>Technique | Comportementale | Ponctualite</c>, which existed in no service and
/// matched nothing in the domain. <c>EvaluationsController</c> cast the service enum across by
/// ordinal, so a <c>MiParcours</c> evaluation was published as <c>Technique</c> and <c>Finale</c> as
/// <c>Comportementale</c> — silently, because an ordinal cast between two enums always compiles.
/// The publisher now maps by name; see <c>EvaluationEventMapping</c>.
/// </remarks>
public enum TypeEvaluation
{
    MiParcours,
    Finale
}

/// <summary>
/// Mirrors <c>Evaluation.Service.Models.StatutEvaluation</c> — keep the members and their order
/// identical.
/// </summary>
/// <remarks>
/// Was <c>EnAttente | Validee | Refusee</c> against the service's
/// <c>EnAttente | Soumise | Validee</c>, so the same ordinal cast published <c>Soumise</c> as
/// <c>Validee</c> and <c>Validee</c> as <c>Refusee</c> — an evaluation announced as approved the
/// moment it was submitted. <c>Refusee</c> was consumed by nothing.
/// </remarks>
public enum StatutEvaluation
{
    EnAttente,
    Soumise,
    Validee
}
