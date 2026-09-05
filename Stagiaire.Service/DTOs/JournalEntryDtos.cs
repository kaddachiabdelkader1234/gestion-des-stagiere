namespace Stagiaire.Service.DTOs;

/// <summary>Payload for creating a journal de bord entry. The stagiaire comes from the route.</summary>
public class JournalEntryCreateDto
{
    /// <summary>The week the entry covers.</summary>
    public DateOnly DateEntree { get; set; }

    /// <summary>What the stagiaire did that week.</summary>
    public string Texte { get; set; } = string.Empty;
}

/// <summary>
/// Payload for editing an entry. A full replacement of the two fields the author owns.
/// </summary>
/// <remarks>
/// Deliberately carries neither <c>StagiaireId</c> nor any comment field. Ownership decides who may
/// read the entry, and a replacement PUT that carried it could null it out and orphan the row — the
/// bug found while scoping conventions in step 5. The comment belongs to the encadrant and has its
/// own endpoint, so it cannot be cleared by the author re-saving their text.
/// </remarks>
public class JournalEntryUpdateDto
{
    public DateOnly DateEntree { get; set; }

    public string Texte { get; set; } = string.Empty;
}

/// <summary>Payload for the encadrant's review note.</summary>
public class JournalCommentaireDto
{
    public string Commentaire { get; set; } = string.Empty;
}

/// <summary>A journal entry as returned to clients.</summary>
public class JournalEntryReadDto
{
    public Guid Id { get; set; }

    public Guid StagiaireId { get; set; }

    public DateOnly DateEntree { get; set; }

    public string Texte { get; set; } = string.Empty;

    public string? CommentaireEncadrant { get; set; }

    public long? CommentaireParId { get; set; }

    public DateTime? DateCommentaire { get; set; }

    public DateTime DateCreation { get; set; }

    public DateTime? DateModification { get; set; }

    /// <summary>
    /// True once the encadrant has reviewed it. Derived rather than stored, so the frontend does not
    /// have to re-derive "is this commented" from a null check on three separate fields.
    /// </summary>
    public bool EstCommentee { get; set; }

    /// <summary>
    /// Whether the entry can still be edited by its author — i.e. it has no comment yet.
    /// </summary>
    /// <remarks>
    /// Advisory, for greying out an edit button. The server enforces the same rule on PUT; a client
    /// that ignores this flag gets a 409, not a silent overwrite.
    /// </remarks>
    public bool Modifiable { get; set; }
}
