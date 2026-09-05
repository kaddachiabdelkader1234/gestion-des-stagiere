using System.ComponentModel.DataAnnotations;

namespace Stagiaire.Service.Models;

/// <summary>
/// A candidature, which becomes the stage record once accepted.
///
/// One entity rather than separate Candidature/Stage tables: the brief's flow only ever promotes a
/// candidature in place (assign département + encadrant, then run the stage), and splitting it would
/// duplicate every identity field for no gain.
/// </summary>
public class Stagiaire
{
    public Guid Id { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the learner who submitted this.
    ///
    /// A BIGINT there, so <c>long</c> here — not a Guid. This is what scopes "a stagiaire sees only
    /// their own candidature": it is taken from the JWT, never from the request body.
    ///
    /// Nullable because an admin may create a record directly for someone with no account yet.
    /// </summary>
    public long? UtilisateurId { get; set; }

    [MaxLength(100)]
    public string Nom { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Prenom { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Département souhaité at submission; the admin may overwrite it when accepting.
    /// </summary>
    [MaxLength(150)]
    public string Departement { get; set; } = string.Empty;

    public TypeStage TypeStage { get; set; } = TypeStage.PFE;

    /// <summary>School or university the candidate attends.</summary>
    [MaxLength(200)]
    public string Ecole { get; set; } = string.Empty;

    /// <summary>
    /// Requested start date, which becomes the actual start date on acceptance. Kept as one field
    /// rather than a separate "souhaitée" pair, since the admin confirms or adjusts these in place.
    /// </summary>
    public DateOnly DateDebut { get; set; }

    public DateOnly DateFin { get; set; }

    /// <summary>Optional free-text motivation supplied with the application.</summary>
    [MaxLength(2000)]
    public string? Motivation { get; set; }

    /// <summary>
    /// Path of the stored CV on the shared volume, relative to the configured storage root.
    /// The file itself is never held in the database — see <c>Storage:CvPath</c>.
    /// </summary>
    [MaxLength(500)]
    public string? CvCheminFichier { get; set; }

    /// <summary>Original upload filename, so a download can restore a sensible name.</summary>
    [MaxLength(255)]
    public string? CvNomFichier { get; set; }

    /// <summary>
    /// Path of the scanned candidature document (university form) on the shared volume.
    /// </summary>
    [MaxLength(500)]
    public string? DocumentCheminFichier { get; set; }

    /// <summary>Original upload filename for the candidature document.</summary>
    [MaxLength(255)]
    public string? DocumentNomFichier { get; set; }

    public StatutStagiaire Statut { get; set; } = StatutStagiaire.EnAttente;

    /// <summary>
    /// auth-service <c>user_id</c> of the assigned TRAINER. Set when the admin accepts.
    ///
    /// NOTE: Evaluation.Service models its own <c>EncadrantId</c> as a <c>Guid</c>, which cannot
    /// reference an auth-service user. That inconsistency needs reconciling in the evaluation phase;
    /// <c>long</c> here matches the actual users table.
    /// </summary>
    public long? EncadrantId { get; set; }

    /// <summary>Denormalized encadrant name, so listing stagiaires needs no call to auth-service.</summary>
    [MaxLength(200)]
    public string? EncadrantNom { get; set; }

    /// <summary>Required when <see cref="Statut"/> is <see cref="StatutStagiaire.Rejetee"/>.</summary>
    [MaxLength(1000)]
    public string? MotifRejet { get; set; }

    /// <summary>When the candidature was submitted (UTC). Drives "newest first" on the admin table.</summary>
    public DateTime DateSoumission { get; set; }

    /// <summary>When the admin accepted or rejected (UTC); null while still pending.</summary>
    public DateTime? DateDecision { get; set; }

    /// <summary>
    /// Journal de bord entries for this stage, cascade-deleted with the record.
    /// Never projected into a read DTO — the journal is fetched through its own endpoint so the list
    /// stays paged.
    /// </summary>
    public ICollection<JournalEntry> JournalEntrees { get; set; } = new List<JournalEntry>();
}
