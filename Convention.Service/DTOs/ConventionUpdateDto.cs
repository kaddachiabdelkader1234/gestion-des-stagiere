using Convention.Service.Models;

namespace Convention.Service.DTOs;

public class ConventionUpdateDto
{
    // Deliberately no UtilisateurId/EncadrantId. Ownership is what scopes every read, so it is set
    // once from CandidatureAccepted (or at create) and never through an edit: a PUT is a full
    // replacement, so a client that simply omitted the fields would silently null them and make the
    // convention invisible to its own stagiaire and encadrant.
    public Guid StagiaireId { get; set; }
    public string StagiaireNom { get; set; } = string.Empty;
    public string StagiairePrenom { get; set; } = string.Empty;
    public string StagiaireEmail { get; set; } = string.Empty;
    public string Departement { get; set; } = string.Empty;
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public DateOnly DateGeneration { get; set; }
    public StatutSignature StatutSignature { get; set; }
    public string? CheminPdf { get; set; }
}