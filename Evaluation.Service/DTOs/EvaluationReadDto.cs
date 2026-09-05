using Evaluation.Service.Models;

namespace Evaluation.Service.DTOs;

/// <summary>
/// An evaluation as returned to clients.
/// </summary>
public class EvaluationReadDto
{
    public Guid Id { get; set; }

    public Guid StagiaireId { get; set; }

    /// <summary>auth-service user id of the learner — what scopes a LEARNER's reads.</summary>
    public long? UtilisateurId { get; set; }

    /// <summary>auth-service user id of the assigned encadrant — what scopes a TRAINER's reads.</summary>
    public long EncadrantId { get; set; }

    /// <summary>Denormalised at create time, so a list needs no lookup per row.</summary>
    public string StagiaireNom { get; set; } = string.Empty;

    public string StagiairePrenom { get; set; } = string.Empty;

    public string StagiaireEmail { get; set; } = string.Empty;

    public TypeEvaluation TypeEvaluation { get; set; }

    public DateOnly DateEvaluation { get; set; }

    public decimal Note { get; set; }

    public string Commentaire { get; set; } = string.Empty;

    public StatutEvaluation Statut { get; set; }
}
