import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { tap, catchError, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { toApiError } from '../http/api-error';

/**
 * Login/register response from auth-service.
 *
 * Verified against the running service — the body is:
 *   { token, type: "Bearer", userId, email, firstName, role, imageBase64, experience, message }
 *
 * Note `role` is a single string (the Java `RoleType` enum name: ADMIN / TRAINER / LEARNER),
 * not an array, and there is no nested `profile` object.
 */
export interface AuthResponse {
  token?: string;
  refreshToken?: string;
  type?: string;
  userId?: number;
  email?: string;
  firstName?: string;
  /** Singular — this is what the API sends. */
  role?: string;
  /** Only present if auth-service is later changed to emit multiple roles. */
  roles?: string[];
  imageBase64?: string;
  experience?: number;
  message?: string;
}

/** The subset of the response we persist as the current user. */
export interface UserProfile {
  userId?: number;
  email?: string;
  firstName?: string;
  role?: string;
  imageBase64?: string;
  experience?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = environment.authApiUrl;
  private token: string | null = null;
  private refreshTokenValue: string | null = null;
  private userProfile: UserProfile | null = null;
  private userRoles: string[] = [];

  constructor(private http: HttpClient) {
    this.init();
  }

  init(): Promise<boolean> {
    const storedToken = localStorage.getItem('auth_token');
    const storedRefresh = localStorage.getItem('auth_refresh_token');
    const storedProfile = localStorage.getItem('auth_profile');
    const storedRoles = localStorage.getItem('auth_roles');

    if (storedToken && storedProfile) {
      this.token = storedToken;
      this.refreshTokenValue = storedRefresh;
      this.userProfile = JSON.parse(storedProfile);
      this.userRoles = storedRoles ? JSON.parse(storedRoles) : [];
      return Promise.resolve(true);
    }

    return Promise.resolve(false);
  }

  setAuthData(token: string, profile: UserProfile, roles: string[], refreshToken?: string): void {
    this.token = token;
    this.refreshTokenValue = refreshToken ?? null;
    this.userProfile = profile;
    this.userRoles = roles;

    localStorage.setItem('auth_token', token);
    if (refreshToken) {
      localStorage.setItem('auth_refresh_token', refreshToken);
    }
    localStorage.setItem('auth_profile', JSON.stringify(profile));
    localStorage.setItem('auth_roles', JSON.stringify(roles));
  }

  login(credentials: { username: string, password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, {
      email: credentials.username,
      password: credentials.password
    }).pipe(
      tap(response => {
        if (response.token) {
          this.setAuthData(response.token, toProfile(response), extractRoles(response), response.refreshToken);
        }
      }),
      // Must rethrow: catching into a normal emission made a rejected login look successful,
      // and sign-in.component navigates to /dashboard on `next`.
      catchError(error => throwError(() => toApiError(error)))
    );
  }

  register(data: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, data).pipe(
      tap(response => {
        if (response.token) {
          this.setAuthData(response.token, toProfile(response), extractRoles(response), response.refreshToken);
        }
      }),
      catchError(error => throwError(() => toApiError(error)))
    );
  }

  fetchUserData(): Observable<UserProfile> {
    return new Observable(subscriber => {
      subscriber.next(this.userProfile ?? {});
      subscriber.complete();
    });
  }

  getUserInfo(): UserProfile | null {
    return this.userProfile;
  }

  logout(): void {
    this.token = null;
    this.refreshTokenValue = null;
    this.userProfile = null;
    this.userRoles = [];
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_refresh_token');
    localStorage.removeItem('auth_profile');
    localStorage.removeItem('auth_roles');
  }

  getRefreshToken(): string | null {
    return this.refreshTokenValue;
  }

  /**
   * Attempts to refresh the access token using the stored refresh token.
   * Returns true if successful, false if the refresh token is expired/invalid.
   */
  refreshAccessToken(): Observable<boolean> {
    const refresh = this.refreshTokenValue;
    if (!refresh) {
      return throwError(() => new Error('No refresh token'));
    }

    return this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, { refreshToken: refresh }).pipe(
      tap(response => {
        if (response.token) {
          this.setAuthData(response.token, toProfile(response), extractRoles(response), response.refreshToken);
        }
      }),
      map(response => !!response.token),
      catchError(() => {
        this.logout();
        return of(false);
      })
    );
  }

  isAuthenticated(): boolean {
    return this.token !== null;
  }

  getToken(): string | null {
    return this.token;
  }

  getUserProfile(): UserProfile | null {
    return this.userProfile;
  }

  getRoles(): string[] {
    return this.userRoles;
  }

  hasRole(role: string): boolean {
    return this.userRoles.includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(role => this.userRoles.includes(role));
  }

  hasAllRoles(roles: string[]): boolean {
    return roles.every(role => this.userRoles.includes(role));
  }

  isAdmin(): boolean {
    return this.hasRole('ADMIN');
  }
}

/**
 * Builds the role list from the response.
 *
 * auth-service sends a singular `role`. Reading only `roles` left this array permanently empty,
 * which silently disabled every check in PermissionService and the permission guard — nothing
 * threw, the menu simply never showed anything. The plural form is still honoured in case
 * auth-service grows multi-role tokens.
 */
function extractRoles(response: AuthResponse): string[] {
  const roles = [
    ...(response.role ? [response.role] : []),
    ...(response.roles ?? [])
  ]
    .filter(role => typeof role === 'string' && role.trim().length > 0)
    .map(role => role.trim().toUpperCase());

  return [...new Set(roles)];
}

function toProfile(response: AuthResponse): UserProfile {
  return {
    userId: response.userId,
    email: response.email,
    firstName: response.firstName,
    role: response.role,
    imageBase64: response.imageBase64,
    experience: response.experience
  };
}
