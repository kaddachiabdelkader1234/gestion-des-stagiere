using Evaluation.Service.Models;

namespace Evaluation.Service.DTOs;

/// <summary>
/// Payload for <c>POST /api/v1/evaluations</c>.
/// </summary>
/// <remarks>
/// Carries neither <c>EncadrantId</c>, <c>UtilisateurId</c>, the stagiaire's name, nor <c>Statut</c>.
/// All four are set server-side:
/// <list type="bullet">
///   <item>the two ids are what visibility is enforced on — taking them from the body is exactly the
///   hole that let a trainer record a note for someone else's stagiaire;</item>
///   <item>the name came from the body, which meant the notification email said whatever the caller
///   typed rather than who the stagiaire is;</item>
///   <item><c>Statut</c> would let an encadrant self-validate — see
///   <c>EvaluationsController.Valider</c>.</item>
/// </list>
/// </remarks>
public class EvaluationCreateDto
{
    /// <summary>The stagiaire being evaluated. Must be one the caller is assigned to.</summary>
    public Guid StagiaireId { get; set; }

    public TypeEvaluation TypeEvaluation { get; set; }

    public DateOnly DateEvaluation { get; set; }

    /// <summary>Score out of 20.</summary>
    public decimal Note { get; set; }

    public string Commentaire { get; set; } = string.Empty;
}
