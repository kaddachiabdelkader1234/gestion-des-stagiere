import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { StagiaireService } from './stagiaire.service';
import { Stagiaire } from '../models/stagiaire.model';
import { PagedResult } from '../models/paged-result.model';
import { ApiError } from '../http/api-error';
import { environment } from '../../../environments/environment';

/** Shaped exactly like StagiaireReadDto: Guid id, `statut`, `typeStage`, no `encadrant` string. */
const STAGIAIRE: Stagiaire = {
  id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  utilisateurId: 7,
  nom: 'Ben Ali',
  prenom: 'Amine',
  email: 'amine@stb.tn',
  departement: 'IT',
  typeStage: 'PFE',
  ecole: 'ENIT',
  dateDebut: '2026-09-01',
  dateFin: '2027-02-28',
  motivation: null,
  cvDisponible: false,
  cvNomFichier: null,
  documentDisponible: false,
  documentNomFichier: null,
  statut: 'EnAttente',
  encadrantId: null,
  encadrantNom: null,
  motifRejet: null,
  dateSoumission: '2026-08-18T10:00:00Z',
  dateDecision: null
};

/** List endpoints return this envelope, not a bare array. */
const PAGE: PagedResult<Stagiaire> = {
  items: [STAGIAIRE],
  totalCount: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false
};

describe('StagiaireService', () => {
  let service: StagiaireService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(StagiaireService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('calls the gateway on the versioned route', () => {
    service.getAll().subscribe();

    // Regression: this used to hit http://localhost:8080/api/Stagiaires — wrong port, wrong path.
    const req = httpMock.expectOne(`${environment.apiUrl}/stagiaires`);
    expect(req.request.url).toBe('http://localhost:18080/api/v1/stagiaires');
    expect(req.request.method).toBe('GET');
    req.flush(PAGE);
  });

  it('returns the paged envelope, not a bare array', () => {
    let received: PagedResult<Stagiaire> | undefined;
    service.getAll().subscribe(page => (received = page));

    httpMock.expectOne(`${environment.apiUrl}/stagiaires`).flush(PAGE);

    expect(received?.items.length).toBe(1);
    expect(received?.totalCount).toBe(1);
    expect(received?.items[0].typeStage).toBe('PFE');
  });

  it('sends only the filters that have a value', () => {
    service.getAll({ page: 2, pageSize: 10, statut: 'EnAttente', departement: '' }).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/stagiaires`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('statut')).toBe('EnAttente');
    // An empty departement must be dropped — `departement=` would be a filter on the empty string.
    expect(req.request.params.has('departement')).toBeFalse();
    req.flush(PAGE);
  });

  it('reads a stagiaire by its Guid', () => {
    let received: Stagiaire | undefined;
    service.getById(STAGIAIRE.id).subscribe(s => (received = s));

    httpMock
      .expectOne(`${environment.apiUrl}/stagiaires/${STAGIAIRE.id}`)
      .flush(STAGIAIRE);

    expect(received?.id).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    expect(received?.statut).toBe('EnAttente');
  });

  it('sends a create payload without an id', () => {
    service
      .create({
        nom: 'Ben Ali',
        prenom: 'Amine',
        email: 'amine@stb.tn',
        departement: 'IT',
        typeStage: 'PFE',
        ecole: 'ENIT',
        dateDebut: '2026-09-01',
        dateFin: '2027-02-28'
      })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/stagiaires`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.id).toBeUndefined();
    req.flush(STAGIAIRE);
  });

  it('surfaces validation errors instead of swallowing them', () => {
    let caught: ApiError | undefined;
    let emitted = false;

    service.getAll().subscribe({
      next: () => (emitted = true),
      error: (error: ApiError) => (caught = error)
    });

    httpMock.expectOne(`${environment.apiUrl}/stagiaires`).flush(
      { error: 'Le département est obligatoire.', code: 'VALIDATION_ERROR' },
      { status: 400, statusText: 'Bad Request' }
    );

    // Previously catchError returned of([]) here, so the table rendered "no stagiaires"
    // for what was actually a failed request.
    expect(emitted).toBeFalse();
    expect(caught?.status).toBe(400);
    expect(caught?.code).toBe('VALIDATION_ERROR');
    expect(caught?.message).toBe('Le département est obligatoire.');
  });

  it('reports a 403 from the gateway in readable terms', () => {
    let caught: ApiError | undefined;

    service.delete(STAGIAIRE.id).subscribe({
      error: (error: ApiError) => (caught = error)
    });

    httpMock
      .expectOne(`${environment.apiUrl}/stagiaires/${STAGIAIRE.id}`)
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(caught?.code).toBe('FORBIDDEN');
    expect(caught?.message).toContain('droits');
  });

  it('reports an unreachable gateway distinctly from a server error', () => {
    let caught: ApiError | undefined;

    service.getAll().subscribe({ error: (error: ApiError) => (caught = error) });

    httpMock
      .expectOne(`${environment.apiUrl}/stagiaires`)
      .error(new ProgressEvent('error'));

    expect(caught?.status).toBe(0);
    expect(caught?.code).toBe('NETWORK_ERROR');
  });
});
