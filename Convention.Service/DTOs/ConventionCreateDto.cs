using System.ComponentModel.DataAnnotations;
using Convention.Service.Models;

namespace Convention.Service.DTOs;

public class ConventionCreateDto
{
    public Guid StagiaireId { get; set; }

    /// <summary>
    /// auth-service user id of the owning learner. Optional, but without it the convention is
    /// visible to admins only — a manually created row has no other way to be scoped.
    /// </summary>
    public long? UtilisateurId { get; set; }

    /// <summary>auth-service user id of the assigned encadrant. Same caveat as above.</summary>
    public long? EncadrantId { get; set; }

    [MaxLength(100)]
    public string StagiaireNom { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string StagiairePrenom { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string StagiaireEmail { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Departement { get; set; } = string.Empty;

    public DateOnly DateDebut { get; set; }

    public DateOnly DateFin { get; set; }

    public DateOnly DateGeneration { get; set; }

    public StatutSignature StatutSignature { get; set; } = StatutSignature.EnAttente;

    /// <summary>Optional: the PDF is generated server-side (see POST /{id}/generer), not supplied here.</summary>
    public string? CheminPdf { get; set; }
}
