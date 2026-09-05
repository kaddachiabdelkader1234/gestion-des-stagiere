using Evaluation.Service.Models;

namespace Evaluation.Service.DTOs;

/// <summary>
/// Payload for <c>PUT /api/v1/evaluations/{id}</c>.
/// </summary>
/// <remarks>
/// Carries none of `StagiaireId`, `EncadrantId`, `UtilisateurId` or `Statut`.
///
/// `PUT` is a full replacement, so a body that carried a column visibility depends on would null it
/// out for any client that had no reason to send it — exactly the bug found while scoping conventions
/// (see `HANDOFF.md` §11), where an admin editing a convention made it invisible to its own stagiaire.
/// The subject of an evaluation cannot be changed by editing it; correcting the wrong stagiaire means
/// deleting and re-creating. `Statut` moves through its own admin-only endpoint.
/// </remarks>
public class EvaluationUpdateDto
{
    public TypeEvaluation TypeEvaluation { get; set; }

    public DateOnly DateEvaluation { get; set; }

    /// <summary>Score out of 20.</summary>
    public decimal Note { get; set; }

    public string Commentaire { get; set; } = string.Empty;
}
