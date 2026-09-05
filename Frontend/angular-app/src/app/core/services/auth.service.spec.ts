import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { AuthService, AuthResponse } from './auth.service';
import { ApiError } from '../http/api-error';
import { environment } from '../../../environments/environment';

/**
 * The response body below matches what the running auth-service actually returns — a singular
 * `role` string, no `roles` array and no nested `profile`.
 */
const LOGIN_RESPONSE: AuthResponse = {
  token: 'header.payload.signature',
  type: 'Bearer',
  userId: 7,
  email: 'amine@stb.tn',
  firstName: 'Amine',
  role: 'TRAINER',
  imageBase64: '',
  experience: 0,
  message: 'Connexion réussie'
};

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('posts login to the gateway auth route', () => {
    service.login({ username: 'amine@stb.tn', password: 'Passw0rd!' }).subscribe();

    const req = httpMock.expectOne(`${environment.authApiUrl}/login`);
    expect(req.request.method).toBe('POST');
    // auth-service expects `email`, while the component collects a `username`.
    expect(req.request.body).toEqual({ email: 'amine@stb.tn', password: 'Passw0rd!' });
    req.flush(LOGIN_RESPONSE);
  });

  it('derives roles from the singular role claim', () => {
    service.login({ username: 'amine@stb.tn', password: 'Passw0rd!' }).subscribe();
    httpMock.expectOne(`${environment.authApiUrl}/login`).flush(LOGIN_RESPONSE);

    // Regression: reading only `response.roles` left this empty, which silently disabled
    // every permission check without raising an error anywhere.
    expect(service.getRoles()).toEqual(['TRAINER']);
    expect(service.hasRole('TRAINER')).toBeTrue();
    expect(service.isAdmin()).toBeFalse();
  });

  it('recognises an admin token', () => {
    service.login({ username: 'rania@stb.tn', password: 'Passw0rd!' }).subscribe();
    httpMock
      .expectOne(`${environment.authApiUrl}/login`)
      .flush({ ...LOGIN_RESPONSE, role: 'ADMIN' });

    expect(service.isAdmin()).toBeTrue();
  });

  it('still accepts a plural roles array if auth-service ever sends one', () => {
    service.login({ username: 'multi@stb.tn', password: 'Passw0rd!' }).subscribe();
    httpMock
      .expectOne(`${environment.authApiUrl}/login`)
      .flush({ ...LOGIN_RESPONSE, role: undefined, roles: ['ADMIN', 'TRAINER'] });

    expect(service.getRoles()).toEqual(['ADMIN', 'TRAINER']);
  });

  it('stores the token and profile so a reload stays authenticated', () => {
    service.login({ username: 'amine@stb.tn', password: 'Passw0rd!' }).subscribe();
    httpMock.expectOne(`${environment.authApiUrl}/login`).flush(LOGIN_RESPONSE);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.getToken()).toBe('header.payload.signature');
    expect(service.getUserProfile()?.email).toBe('amine@stb.tn');
    expect(localStorage.getItem('auth_token')).toBe('header.payload.signature');
  });

  it('reports a rejected login as an error and stays unauthenticated', () => {
    let caught: ApiError | undefined;
    let completedNormally = false;

    service.login({ username: 'amine@stb.tn', password: 'wrong' }).subscribe({
      next: () => (completedNormally = true),
      error: (error: ApiError) => (caught = error)
    });

    httpMock
      .expectOne(`${environment.authApiUrl}/login`)
      .flush({ message: 'Email ou mot de passe incorrect' }, { status: 401, statusText: 'Unauthorized' });

    // Previously this arrived on `next`, so sign-in.component navigated to /dashboard
    // even though no token had been issued.
    expect(completedNormally).toBeFalse();
    expect(caught?.status).toBe(401);
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('clears stored credentials on logout', () => {
    service.login({ username: 'amine@stb.tn', password: 'Passw0rd!' }).subscribe();
    httpMock.expectOne(`${environment.authApiUrl}/login`).flush(LOGIN_RESPONSE);

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getRoles()).toEqual([]);
    expect(localStorage.getItem('auth_token')).toBeNull();
  });
});
