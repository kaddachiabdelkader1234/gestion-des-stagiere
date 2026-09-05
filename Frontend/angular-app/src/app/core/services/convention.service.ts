import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import { PagedResult } from '../models/paged-result.model';
import { Convention, ConventionQuery } from '../models/convention.model';
import { toHttpParams } from './stagiaire.service';

/**
 * Talks to Convention.Service through the API Gateway.
 *
 * The gateway allows GET for any authenticated user and restricts every write to ADMIN, so
 * `generer` and `signer` will 403 for a learner or trainer — the UI hides them rather than
 * relying on that.
 *
 * Reads are additionally scoped server-side by role: an ADMIN sees every convention, a TRAINER only
 * those of their assigned stagiaires, and a LEARNER only their own. Passing someone else's
 * `stagiaireId` cannot widen that, and an out-of-scope id returns 404 rather than 403.
 */
@Injectable({
  providedIn: 'root'
})
export class ConventionService {
  private readonly baseUrl = `${environment.apiUrl}/conventions`;

  constructor(private http: HttpClient) {}

  /**
   * Lists conventions, newest first. Returns a paged envelope — read `.items`.
   *
   * Already role-scoped by the server, so an admin screen and a learner screen can share this call.
   */
  getAll(query: ConventionQuery = {}): Observable<PagedResult<Convention>> {
    return this.http
      .get<PagedResult<Convention>>(this.baseUrl, { params: toHttpParams(query) })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  getById(id: string): Observable<Convention> {
    return this.http
      .get<Convention>(`${this.baseUrl}/${id}`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Admin: renders the PDF server-side from the stored stagiaire details and saves it to the
   * storage volume. Safe to call again — it replaces the previous PDF rather than adding one.
   */
  generer(id: string): Observable<Convention> {
    return this.http
      .post<Convention>(`${this.baseUrl}/${id}/generer`, {})
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Downloads the generated PDF as a blob. 404 means it has not been generated yet — check
   * `pdfDisponible` before offering this.
   */
  telechargerPdf(id: string): Observable<Blob> {
    return this.http
      .get(`${this.baseUrl}/${id}/pdf`, { responseType: 'blob' })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Admin: marks the convention signed (Brouillon → Signée). No e-signature — a deliberate
   * decision for this project. Returns 409 if it is already signed.
   */
  signer(id: string): Observable<Convention> {
    return this.http
      .post<Convention>(`${this.baseUrl}/${id}/signer`, {})
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }
}
