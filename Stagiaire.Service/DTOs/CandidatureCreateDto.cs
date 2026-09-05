using Stagiaire.Service.Models;

namespace Stagiaire.Service.DTOs;

/// <summary>
/// Body for <c>POST /api/v1/candidatures</c> — a learner applying for an internship.
///
/// Deliberately has no <c>Statut</c>, <c>EncadrantId</c> or <c>UtilisateurId</c>: a new candidature
/// is always <c>EnAttente</c>, the encadrant is assigned by an admin, and the owner is taken from
/// the JWT. Accepting any of them from the body would let a caller self-approve or file a
/// candidature in someone else's name.
/// </summary>
public class CandidatureCreateDto
{
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;

    /// <summary>
    /// Contact email. May differ from the account email — the account is identified by the JWT.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Département souhaité.</summary>
    public string Departement { get; set; } = string.Empty;

    public TypeStage TypeStage { get; set; } = TypeStage.PFE;

    public string Ecole { get; set; } = string.Empty;

    /// <summary>Date de début souhaitée.</summary>
    public DateOnly DateDebut { get; set; }

    public DateOnly DateFin { get; set; }

    public string? Motivation { get; set; }
}
