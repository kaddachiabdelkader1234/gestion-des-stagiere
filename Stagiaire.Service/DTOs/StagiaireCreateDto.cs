using Stagiaire.Service.Models;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// Body for <c>POST /api/v1/stagiaires</c> — the admin creating a record directly.
///
/// A learner applying should use <see cref="CandidatureCreateDto"/> via
/// <c>POST /api/v1/candidatures</c> instead; this one allows setting the status, which an applicant
/// must not control.
/// </summary>
public class StagiaireCreateDto
{
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Departement { get; set; } = string.Empty;
    public TypeStage TypeStage { get; set; } = TypeStage.PFE;
    public string Ecole { get; set; } = string.Empty;
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public string? Motivation { get; set; }
    public StatutStagiaire Statut { get; set; } = StatutStagiaire.EnAttente;

    /// <summary>Link to an existing auth-service account, if there is one.</summary>
    public long? UtilisateurId { get; set; }
}
