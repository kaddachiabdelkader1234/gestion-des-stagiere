import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';
import {
  CandidatureAccepterRequest,
  CandidatureCreateRequest,
  CandidatureRejeterRequest,
  Stagiaire
} from '../models/stagiaire.model';

/**
 * Candidature lifecycle against Stagiaire.Service, via the gateway.
 *
 * Separate from {@link StagiaireService} (plain CRUD) because these are state transitions with
 * server-side side effects — accepting publishes a RabbitMQ event and triggers the convention.
 */
@Injectable({
  providedIn: 'root'
})
export class CandidatureService {
  private readonly baseUrl = `${environment.apiUrl}/candidatures`;

  constructor(private http: HttpClient) {}

  /**
   * Submits a candidature. The owner is taken from the JWT server-side, and the status is always
   * `EnAttente` — neither can be set from here.
   */
  soumettre(candidature: CandidatureCreateRequest): Observable<Stagiaire> {
    return this.http
      .post<Stagiaire>(this.baseUrl, candidature)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** The caller's own candidature. 404 means they have not applied yet — expected, not an error. */
  maCandidature(): Observable<Stagiaire> {
    return this.http
      .get<Stagiaire>(`${this.baseUrl}/moi`)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Uploads (or replaces) the CV.
   *
   * Sent as multipart under the field name `fichier`. Deliberately no Content-Type header: the
   * browser must set it, because it has to append the multipart boundary.
   */
  televerserCv(id: string, fichier: File): Observable<Stagiaire> {
    const formData = new FormData();
    formData.append('fichier', fichier, fichier.name);

    return this.http
      .post<Stagiaire>(`${this.baseUrl}/${id}/cv`, formData)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * Downloads the CV as a blob, for `URL.createObjectURL` or a save prompt.
   */
  telechargerCv(id: string): Observable<Blob> {
    return this.http
      .get(`${this.baseUrl}/${id}/cv`, { responseType: 'blob' })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Uploads (or replaces) the candidature document (university scan). */
  televerserDocument(id: string, fichier: File): Observable<Stagiaire> {
    const formData = new FormData();
    formData.append('fichier', fichier, fichier.name);
    return this.http
      .post<Stagiaire>(`${this.baseUrl}/${id}/document`, formData)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Downloads the candidature document as a blob. */
  telechargerDocument(id: string): Observable<Blob> {
    return this.http
      .get(`${this.baseUrl}/${id}/document`, { responseType: 'blob' })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Admin: accept, assigning département and encadrant. */
  accepter(id: string, decision: CandidatureAccepterRequest): Observable<Stagiaire> {
    return this.http
      .post<Stagiaire>(`${this.baseUrl}/${id}/accepter`, decision)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /** Admin: reject with a reason (the server requires at least 10 characters). */
  rejeter(id: string, decision: CandidatureRejeterRequest): Observable<Stagiaire> {
    return this.http
      .post<Stagiaire>(`${this.baseUrl}/${id}/rejeter`, decision)
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }
}
