using Convention.Service.Models;

namespace Convention.Service.DTOs;

public class ConventionReadDto
{
    public Guid Id { get; set; }
    public Guid StagiaireId { get; set; }
    /// <summary>auth-service user id of the owning learner. Null on rows with no linked account.</summary>
    public long? UtilisateurId { get; set; }
    /// <summary>auth-service user id of the assigned encadrant.</summary>
    public long? EncadrantId { get; set; }
    public string StagiaireNom { get; set; } = string.Empty;
    public string StagiairePrenom { get; set; } = string.Empty;
    public string StagiaireEmail { get; set; } = string.Empty;
    public string Departement { get; set; } = string.Empty;
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public DateOnly DateGeneration { get; set; }
    public StatutSignature StatutSignature { get; set; }
    /// <summary>True once the PDF has been generated and is downloadable at /api/v1/conventions/{id}/pdf.</summary>
    public bool PdfDisponible { get; set; }
}