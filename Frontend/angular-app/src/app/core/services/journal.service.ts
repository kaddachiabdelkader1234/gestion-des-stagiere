import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import { PagedResult } from '../models/paged-result.model';
import {
  JournalCommentaireRequest,
  JournalEntry,
  JournalEntryCreateRequest,
  JournalEntryUpdateRequest,
  JournalQuery
} from '../models/journal.model';
import { toHttpParams } from './stagiaire.service';

/**
 * Talks to the journal de bord sub-resource of Stagiaire.Service through the API Gateway.
 *
 * Every call is nested under a stagiaire, because that is where authorisation happens: the entries
 * are visible to that stagiaire, their assigned encadrant, and an admin. The gateway permits all
 * three roles on these routes, so the boundary is enforced inside the service — an out-of-scope
 * stagiaire returns **404, not 403**, which is why `getAll` failing with 404 means "not yours",
 * not "no entries".
 *
 * Who may do what:
 * - write/edit an entry — the stagiaire who owns the stage, or an admin (`403` for an encadrant)
 * - comment — the assigned encadrant, or an admin
 * - delete — admin only, enforced at the gateway too
 */
@Injectable({
  providedIn: 'root'
})
export class JournalService {
  constructor(private http: HttpClient) {}

  /** Lists a stagiaire's entries, newest week first. Returns a paged envelope — read `.items`. */
  getAll(stagiaireId: string, query: JournalQuery = {}): Observable<PagedResult<JournalEntry>> {
    return this.http
      .get<PagedResult<JournalEntry>>(this.baseUrl(stagiaireId), { params: toHttpParams(query) })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  getById(stagiaireId: string, entryId: string): Observable<JournalEntry> {
    return this.http
      .get<JournalEntry>(`${this.baseUrl(stagiaireId)}/${entryId}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Adds a weekly entry.
   *
   * `409` means either that week already has an entry, or the candidature is not accepted yet —
   * the journal only opens once there is a stage to journal about.
   */
  creer(stagiaireId: string, body: JournalEntryCreateRequest): Observable<JournalEntry> {
    return this.http
      .post<JournalEntry>(this.baseUrl(stagiaireId), body)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Corrects an entry. PUT returns 204 — there is no response body, so refetch to show the result.
   *
   * `409` once the encadrant has commented: the entry and the comment are then one review record.
   * Check {@link JournalEntry.modifiable} before offering this.
   */
  modifier(stagiaireId: string, entryId: string, body: JournalEntryUpdateRequest): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl(stagiaireId)}/${entryId}`, body)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Records the encadrant's review note and returns the updated entry.
   *
   * Re-posting replaces the note rather than conflicting, so the same call serves "comment" and
   * "correct my comment". Doing so freezes the stagiaire's text.
   */
  commenter(
    stagiaireId: string,
    entryId: string,
    body: JournalCommentaireRequest
  ): Observable<JournalEntry> {
    return this.http
      .post<JournalEntry>(`${this.baseUrl(stagiaireId)}/${entryId}/commentaire`, body)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Admin only — the gateway rejects a non-admin DELETE with 403 before it reaches the service. */
  supprimer(stagiaireId: string, entryId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl(stagiaireId)}/${entryId}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  private baseUrl(stagiaireId: string): string {
    return `${environment.apiUrl}/stagiaires/${stagiaireId}/journal`;
  }
}
