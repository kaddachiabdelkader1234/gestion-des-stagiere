import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';

/**
 * A user account as returned by `GET /api/v1/auth/users?role=…`.
 *
 * Mirrors auth-service `UserSummaryResponse` — no token, no profile image.
 */
export interface UserSummary {
  userId: number;
  email: string;
  firstName: string;
  role: string;
}

/**
 * Reads user accounts from auth-service through the gateway.
 *
 * ADMIN only: the gateway restricts `/api/v1/auth/users` to `ROLE_ADMIN`, since it exposes account
 * data. A non-admin caller gets a 403.
 */
@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly baseUrl = `${environment.authApiUrl}/users`;

  /** The encadrant dropdown is opened repeatedly; cache the list for the session. */
  private trainers$?: Observable<UserSummary[]>;

  constructor(private http: HttpClient) {}

  getByRole(role: string): Observable<UserSummary[]> {
    return this.http
      .get<UserSummary[]>(this.baseUrl, { params: new HttpParams().set('role', role) })
      .pipe(catchError(error => throwError(() => toApiError(error))));
  }

  /**
   * TRAINER accounts, for the "assign an encadrant" picker.
   *
   * @param refresh pass true after a new trainer registers, to bypass the cache.
   */
  getTrainers(refresh = false): Observable<UserSummary[]> {
    if (refresh || !this.trainers$) {
      this.trainers$ = this.getByRole('TRAINER').pipe(shareReplay(1));
    }

    return this.trainers$;
  }
}
