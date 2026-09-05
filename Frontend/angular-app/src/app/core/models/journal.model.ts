/**
 * Mirrors Stagiaire.Service's journal DTOs. Keep this file in sync with:
 *   Stagiaire.Service/DTOs/JournalEntryDtos.cs
 *   Stagiaire.Service/DTOs/JournalQueryParameters.cs
 *   Stagiaire.Service/Validation/JournalValidators.cs  (the limits below)
 */

/**
 * One weekly journal de bord entry, as returned by
 * GET /api/v1/stagiaires/{stagiaireId}/journal.
 */
export interface JournalEntry {
  id: string;
  stagiaireId: string;

  /** The week the entry covers. ISO "yyyy-MM-dd" — the API uses DateOnly, so no time component. */
  dateEntree: string;

  texte: string;

  /** The encadrant's review note, or null while unreviewed. */
  commentaireEncadrant?: string | null;

  /** auth-service user id of whoever wrote the comment. */
  commentaireParId?: number | null;

  dateCommentaire?: string | null;

  dateCreation: string;

  /** Null when the entry has never been edited. */
  dateModification?: string | null;

  /** Server-derived: true once the encadrant has commented. */
  estCommentee: boolean;

  /**
   * Server-derived: whether the author may still edit the text.
   *
   * Advisory only — for greying out an edit button. The server enforces the same rule on PUT, so a
   * UI that ignores this gets a 409 rather than silently overwriting a reviewed entry.
   */
  modifiable: boolean;
}

/** Body for POST /api/v1/stagiaires/{stagiaireId}/journal. */
export interface JournalEntryCreateRequest {
  dateEntree: string;
  texte: string;
}

/**
 * Body for PUT /api/v1/stagiaires/{stagiaireId}/journal/{entryId}.
 *
 * Deliberately carries no `stagiaireId` and no comment fields: ownership decides who may read the
 * entry, and a replacement PUT that carried it could null it out and orphan the row.
 */
export interface JournalEntryUpdateRequest {
  dateEntree: string;
  texte: string;
}

/** Body for POST /api/v1/stagiaires/{stagiaireId}/journal/{entryId}/commentaire. */
export interface JournalCommentaireRequest {
  commentaire: string;
}

/** Query parameters for GET /api/v1/stagiaires/{stagiaireId}/journal. */
export interface JournalQuery {
  page?: number;
  pageSize?: number;
  /** Only entries whose week falls on or after this date, "yyyy-MM-dd". */
  du?: string;
  au?: string;
  /** Only entries the encadrant has not reviewed yet. */
  sansCommentaire?: boolean;
}

/**
 * Server-side limits, mirrored here so a form can reject bad input before a round-trip.
 * The server remains the authority — these only save the user a failed request.
 */
export const JOURNAL_LIMITS = {
  texteMin: 10,
  texteMax: 4000,
  commentaireMin: 3,
  commentaireMax: 2000,
  /** An entry may not be dated more than this many days ahead — catches a mistyped year. */
  joursAvanceMax: 7
} as const;
