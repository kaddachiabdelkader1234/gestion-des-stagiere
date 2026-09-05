using System.ComponentModel.DataAnnotations;

namespace Stagiaire.Service.Models;

/// <summary>
/// One weekly journal de bord entry, written by the stagiaire and reviewable by their encadrant.
///
/// A sub-resource of <see cref="Stagiaire"/> rather than its own service: the brief calls for simple
/// CRUD, and every read of an entry has to be authorised against the parent stagiaire's owner and
/// assigned encadrant anyway — putting it elsewhere would mean a cross-service call on every request.
/// </summary>
public class JournalEntry
{
    public Guid Id { get; set; }

    /// <summary>
    /// Parent stagiaire. Cascade-deleted with it: entries have no meaning without the stage, and
    /// leaving them behind would strand personal notes with no owner to authorise reads against.
    /// </summary>
    public Guid StagiaireId { get; set; }

    /// <summary>
    /// The week the entry covers, not the moment it was typed — that is <see cref="DateCreation"/>.
    ///
    /// Unique per stagiaire (see <c>AppDbContext</c>), so a double-submit cannot produce two entries
    /// for the same week. A <c>DateOnly</c> because a week is identified by a calendar date; carrying
    /// a time would make two entries for "the same week" compare unequal.
    /// </summary>
    public DateOnly DateEntree { get; set; }

    /// <summary>What the stagiaire did that week.</summary>
    [MaxLength(4000)]
    public string Texte { get; set; } = string.Empty;

    /// <summary>
    /// The encadrant's review note, or null while unreviewed.
    ///
    /// Its presence freezes the entry: a stagiaire may correct their own text right up until the
    /// encadrant has commented, after which the pair is a review record and editing the text would
    /// leave the comment answering something that was never written.
    /// </summary>
    [MaxLength(2000)]
    public string? CommentaireEncadrant { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of whoever wrote the comment — normally the assigned
    /// encadrant, possibly an admin. Recorded because "commented by an admin" and "commented by the
    /// supervisor" are different facts to anyone auditing the review later.
    /// </summary>
    public long? CommentaireParId { get; set; }

    /// <summary>When the comment was written (UTC); null while unreviewed.</summary>
    public DateTime? DateCommentaire { get; set; }

    /// <summary>When the entry was first submitted (UTC).</summary>
    public DateTime DateCreation { get; set; }

    /// <summary>When the text was last edited (UTC); null if never edited.</summary>
    public DateTime? DateModification { get; set; }

    /// <summary>Navigation to the parent, used to configure the cascade.</summary>
    public Stagiaire? Stagiaire { get; set; }
}
