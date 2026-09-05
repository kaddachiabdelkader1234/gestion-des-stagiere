using Stagiaire.Service.Models;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// Body for <c>PUT /api/v1/stagiaires/{id}</c> — a full replacement by an admin.
///
/// Accept/reject transitions have their own endpoints
/// (<c>/candidatures/{id}/accepter</c>, <c>/rejeter</c>) because they carry extra data and side
/// effects; this is for correcting field values.
/// </summary>
public class StagiaireUpdateDto
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
    public StatutStagiaire Statut { get; set; }
    public long? EncadrantId { get; set; }
    public string? EncadrantNom { get; set; }
    public string? MotifRejet { get; set; }
}
