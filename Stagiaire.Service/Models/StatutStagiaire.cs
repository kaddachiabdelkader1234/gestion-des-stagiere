namespace Stagiaire.Service.Models;

/// <summary>
/// Lifecycle of a candidature, then of the stage itself.
///
/// The brief lists these as two tracks (candidature EN_ATTENTE → ACCEPTEE → REJETEE, stage
/// EN_COURS → TERMINE); they are modelled as one linear enum because a record only ever moves
/// forward through them: EnAttente → Acceptee → EnCours → Termine, or EnAttente → Rejetee.
/// The convention and evaluation tracks live in their own services
/// (Convention.Service StatutSignature, Evaluation.Service StatutEvaluation).
///
/// Serialized as the member name (Program.cs registers JsonStringEnumConverter) and persisted as a
/// string via HasConversion in AppDbContext, so these names are part of the public API contract.
/// Keep in sync with Frontend/angular-app/src/app/core/models/stagiaire.model.ts.
/// </summary>
public enum StatutStagiaire
{
    /// <summary>Candidature submitted, awaiting an admin decision.</summary>
    EnAttente,

    /// <summary>Admin accepted: département and encadrant assigned, convention to be drafted.</summary>
    Acceptee,

    /// <summary>Admin rejected, with a reason in <c>MotifRejet</c>.</summary>
    Rejetee,

    /// <summary>Stage under way.</summary>
    EnCours,

    /// <summary>Stage finished.</summary>
    Termine
}
