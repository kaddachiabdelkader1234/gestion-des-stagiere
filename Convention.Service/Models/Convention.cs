using System.ComponentModel.DataAnnotations;

namespace Convention.Service.Models;

public class Convention
{
    public Guid Id { get; set; }

    public Guid StagiaireId { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the learner this convention belongs to, copied from
    /// <c>CandidatureAccepted</c>.
    ///
    /// This is the security boundary behind "a stagiaire only ever sees their own convention": it is
    /// compared against the JWT's <c>userId</c>, never against anything the client sends.
    ///
    /// Nullable because an admin may create a convention for a record with no linked account, and
    /// because rows written before this column existed have no value — both cases are invisible to
    /// non-admins, which is the safe failure direction.
    /// </summary>
    public long? UtilisateurId { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the assigned encadrant, for the equivalent
    /// "an encadrant only sees their own assigned stagiaires" boundary.
    /// </summary>
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

    [MaxLength(500)]
    public string CheminPdf { get; set; } = string.Empty;
}