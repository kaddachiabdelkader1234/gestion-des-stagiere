import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import { PagedResult } from '../models/paged-result.model';
import {
  Stagiaire,
  StagiaireCreateRequest,
  StagiaireQuery,
  StagiaireUpdateRequest
} from '../models/stagiaire.model';

// Re-exported so existing imports of `Stagiaire` from this service keep working now that the
// interface lives in core/models alongside the other domain models.
export type { Stagiaire, StagiaireCreateRequest, StagiaireUpdateRequest };

/**
 * Talks to Stagiaire.Service through the API Gateway.
 *
 * Everything goes through `environment.apiUrl` (:18080) on the versioned `/api/v1/stagiaires`
 * route, so the Authorization header added by authInterceptor is validated by the gateway.
 *
 * Errors are propagated, not swallowed: a failed request must surface to the caller rather than
 * resolving to an empty list the UI would render as "no stagiaires".
 */
@Injectable({
  providedIn: 'root'
})
export class StagiaireService {
  private readonly baseUrl = `${environment.apiUrl}/stagiaires`;

  constructor(private http: HttpClient) {}

  /**
   * Lists stagiaires. Returns a paged envelope — read `.items`, not the response itself.
   *
   * The server scopes results by role regardless of what is passed here: a TRAINER only ever gets
   * their own assigned stagiaires, a LEARNER only their own record.
   */
  getAll(query: StagiaireQuery = {}): Observable<PagedResult<Stagiaire>> {
    return this.http
      .get<PagedResult<Stagiaire>>(this.baseUrl, { params: toHttpParams(query) })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** @param id Guid string from {@link Stagiaire.id}. */
  getById(id: string): Observable<Stagiaire> {
    return this.http
      .get<Stagiaire>(`${this.baseUrl}/${id}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Admin-only. A learner applies via {@link CandidatureService.soumettre} instead. */
  create(stagiaire: StagiaireCreateRequest): Observable<Stagiaire> {
    return this.http
      .post<Stagiaire>(this.baseUrl, stagiaire)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** PUT replaces the whole record and returns 204 — there is no response body. */
  update(id: string, stagiaire: StagiaireUpdateRequest): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/${id}`, stagiaire)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${id}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }
}

/**
 * Builds query params, dropping empty values so the URL carries only real filters.
 *
 * Sending `statut=` (empty) would be rejected as an invalid enum value rather than ignored.
 */
export function toHttpParams<T extends object>(query: T): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
