import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import { PagedResult } from '../models/paged-result.model';
import {
  Evaluation,
  EvaluationCreateRequest,
  EvaluationQuery,
  EvaluationUpdateRequest
} from '../models/evaluation.model';
import { toHttpParams } from './stagiaire.service';

/**
 * Talks to Evaluation.Service through the API Gateway.
 *
 * The gateway allows GET for any authenticated user and restricts every write to TRAINER + ADMIN,
 * so `creer` and `modifier` will 403 for a learner — the UI hides them rather than relying on that.
 *
 * Reads are additionally scoped server-side by role: an ADMIN sees every evaluation, a TRAINER only
 * those of their assigned stagiaires, and a LEARNER only their own result. Passing someone else's
 * `stagiaireId` cannot widen that, and an out-of-scope id returns 404 rather than 403.
 *
 * Who may do what:
 * - create — TRAINER or ADMIN (the assigned encadrant, or an admin on their behalf)
 * - update — TRAINER or ADMIN, only before validation
 * - validate (admin-only): POST /{id}/valider
 * - delete — ADMIN only
 * - read — any authenticated user, scoped by role
 */
@Injectable({
  providedIn: 'root'
})
export class EvaluationService {
  private readonly baseUrl = `${environment.apiUrl}/evaluations`;

  constructor(private http: HttpClient) {}

  /** Lists evaluations, newest first. Returns a paged envelope — read `.items`. */
  getAll(query: EvaluationQuery = {}): Observable<PagedResult<Evaluation>> {
    return this.http
      .get<PagedResult<Evaluation>>(this.baseUrl, { params: toHttpParams(query) })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  getById(id: string): Observable<Evaluation> {
    return this.http
      .get<Evaluation>(`${this.baseUrl}/${id}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** TRAINER or ADMIN: records an evaluation for a stagiaire. */
  creer(body: EvaluationCreateRequest): Observable<Evaluation> {
    return this.http
      .post<Evaluation>(this.baseUrl, body)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** TRAINER or ADMIN: corrects an evaluation before validation. PUT returns 204. */
  modifier(id: string, body: EvaluationUpdateRequest): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/${id}`, body)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** ADMIN only: validates a submitted evaluation (Soumise → Validee). */
  valider(id: string): Observable<Evaluation> {
    return this.http
      .post<Evaluation>(`${this.baseUrl}/${id}/valider`, {})
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** ADMIN only. */
  supprimer(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${id}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }
}
