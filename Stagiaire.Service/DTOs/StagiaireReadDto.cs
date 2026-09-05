using Stagiaire.Service.Models;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// A stagiaire/candidature as returned by the API.
///
/// Keep in sync with Frontend/angular-app/src/app/core/models/stagiaire.model.ts.
/// </summary>
public class StagiaireReadDto
{
    public Guid Id { get; set; }
    public long? UtilisateurId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Departement { get; set; } = string.Empty;
    public TypeStage TypeStage { get; set; }
    public string Ecole { get; set; } = string.Empty;
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public string? Motivation { get; set; }

    /// <summary>True when a CV was uploaded; fetch it from <c>GET /{id}/cv</c>.</summary>
    public bool CvDisponible { get; set; }

    /// <summary>Original filename, for display next to the download link.</summary>
    public string? CvNomFichier { get; set; }

    /// <summary>True when the candidature document (university scan) was uploaded.</summary>
    public bool DocumentDisponible { get; set; }

    /// <summary>Original filename for the candidature document.</summary>
    public string? DocumentNomFichier { get; set; }

    public StatutStagiaire Statut { get; set; }
    public long? EncadrantId { get; set; }
    public string? EncadrantNom { get; set; }
    public string? MotifRejet { get; set; }
    public DateTime DateSoumission { get; set; }
    public DateTime? DateDecision { get; set; }
}
