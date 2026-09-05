using System.ComponentModel.DataAnnotations;

namespace Evaluation.Service.Models;

/// <summary>
/// Local projection of "who owns and who supervises this stagiaire", built from the
/// <c>CandidatureAccepted</c> event.
///
/// This exists because authorisation cannot be done without it. An evaluation must be scoped to the
/// learner it is about and the encadrant assigned to them, but those identities live in
/// Stagiaire.Service while the only thing a client sends is a <c>StagiaireId</c>. Trusting the body
/// for ownership is precisely the hole this closes, so the ids have to come from somewhere the caller
/// cannot influence.
///
/// The alternative — calling Stagiaire.Service on every request — would put a synchronous dependency
/// in the read path of every evaluation. Convention.Service already denormalises the same ids from
/// the same event (see its <c>CandidatureAcceptedConsumer</c>); this follows that precedent, except
/// that an evaluation is created later by a human rather than by the event itself, so the ids must be
/// kept here to be looked up at that moment.
/// </summary>
public class StagiaireAffectation
{
    /// <summary>The stagiaire record's Guid, as it appears in Stagiaire.Service.</summary>
    [Key]
    public Guid StagiaireId { get; set; }

    /// <summary>auth-service user id of the learner, or null if they have no account.</summary>
    public long? UtilisateurId { get; set; }

    /// <summary>auth-service user id of the assigned encadrant.</summary>
    public long? EncadrantId { get; set; }

    /// <summary>
    /// Denormalised so the <c>EvaluationSubmitted</c> event can carry the stagiaire's name without
    /// the client supplying it — it used to come from the request body, which meant the notification
    /// email said whatever the caller typed.
    /// </summary>
    [MaxLength(100)]
    public string Nom { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Prenom { get; set; } = string.Empty;

    /// <summary>Denormalised email for sending notifications without calling back into Stagiaire.Service.</summary>
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>When this projection row was last written from an event (UTC).</summary>
    public DateTime DateMiseAJour { get; set; }
}
