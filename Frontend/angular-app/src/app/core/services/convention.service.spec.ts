import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { ConventionService } from './convention.service';
import { Convention } from '../models/convention.model';
import { PagedResult } from '../models/paged-result.model';
import { ApiError } from '../http/api-error';
import { environment } from '../../../environments/environment';

/** Shaped exactly like ConventionReadDto — note there is no `cheminPdf`, only `pdfDisponible`. */
const CONVENTION: Convention = {
  id: '9c1f0e2a-1111-4c3d-9b7e-8f5a2d4e6b01',
  stagiaireId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  utilisateurId: 7,
  encadrantId: 12,
  stagiaireNom: 'Ben Ali',
  stagiairePrenom: 'Amine',
  stagiaireEmail: 'amine@stb.tn',
  departement: 'IT',
  dateDebut: '2026-09-01',
  dateFin: '2027-02-28',
  dateGeneration: '2026-08-18',
  statutSignature: 'EnAttente',
  pdfDisponible: false
};

const PAGE: PagedResult<Convention> = {
  items: [CONVENTION],
  totalCount: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false
};

describe('ConventionService', () => {
  let service: ConventionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ConventionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('calls the gateway on the versioned route', () => {
    service.getAll().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/conventions`);
    expect(req.request.url).toBe('http://localhost:18080/api/v1/conventions');
    expect(req.request.method).toBe('GET');
    req.flush(PAGE);
  });

  it('returns the paged envelope, not a bare array', () => {
    let received: PagedResult<Convention> | undefined;
    service.getAll().subscribe(page => (received = page));

    httpMock.expectOne(`${environment.apiUrl}/conventions`).flush(PAGE);

    expect(received?.items.length).toBe(1);
    expect(received?.totalCount).toBe(1);
    expect(received?.items[0].statutSignature).toBe('EnAttente');
  });

  it('sends only the filters that have a value', () => {
    service.getAll({ page: 2, pageSize: 10, statutSignature: 'Signee', stagiaireId: '' }).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/conventions`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('statutSignature')).toBe('Signee');
    // An empty stagiaireId must be dropped — the server would reject it as an invalid Guid.
    expect(req.request.params.has('stagiaireId')).toBeFalse();
    req.flush(PAGE);
  });

  it('scopes the learner view by stagiaireId', () => {
    service.getAll({ stagiaireId: CONVENTION.stagiaireId, pageSize: 1 }).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/conventions`);
    expect(req.request.params.get('stagiaireId')).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    req.flush(PAGE);
  });

  it('generates the PDF with an empty POST body', () => {
    let received: Convention | undefined;
    service.generer(CONVENTION.id).subscribe(c => (received = c));

    const req = httpMock.expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/generer`);
    expect(req.request.method).toBe('POST');
    // The PDF is rendered server-side from the stored row; there is nothing to send.
    expect(req.request.body).toEqual({});
    req.flush({ ...CONVENTION, pdfDisponible: true });

    expect(received?.pdfDisponible).toBeTrue();
  });

  it('requests the PDF as a blob, not parsed as JSON', () => {
    service.telechargerPdf(CONVENTION.id).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/pdf`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }));
  });

  it('marks a convention signed', () => {
    let received: Convention | undefined;
    service.signer(CONVENTION.id).subscribe(c => (received = c));

    const req = httpMock.expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/signer`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...CONVENTION, statutSignature: 'Signee' });

    expect(received?.statutSignature).toBe('Signee');
  });

  it('surfaces a 409 on an already-signed convention instead of swallowing it', () => {
    let caught: ApiError | undefined;
    let emitted = false;

    service.signer(CONVENTION.id).subscribe({
      next: () => (emitted = true),
      error: (error: ApiError) => (caught = error)
    });

    httpMock.expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/signer`).flush(
      { error: 'La convention est déjà signée.', code: 'CONFLICT' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(emitted).toBeFalse();
    expect(caught?.status).toBe(409);
    expect(caught?.message).toBe('La convention est déjà signée.');
  });

  it('surfaces a 404 when the PDF has not been generated yet', () => {
    let caught: ApiError | undefined;

    service.telechargerPdf(CONVENTION.id).subscribe({
      error: (error: ApiError) => (caught = error)
    });

    // On a responseType:'blob' request the browser hands back the error body as a Blob, so the
    // service's JSON error shape ({ error, code }) cannot be read out of it synchronously —
    // toApiError falls back to a generic message. The status is still authoritative, and the UI
    // gates the download on `pdfDisponible` rather than relying on this message.
    httpMock.expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/pdf`).flush(
      new Blob([JSON.stringify({ error: "Pas encore de PDF.", code: 'NOT_FOUND' })]),
      { status: 404, statusText: 'Not Found' }
    );

    expect(caught?.status).toBe(404);
    expect(caught?.message).toBe('La requête a échoué.');
  });

  it('reports a 403 from the gateway in readable terms', () => {
    let caught: ApiError | undefined;

    // The gateway restricts every write on /api/v1/conventions/** to ADMIN.
    service.generer(CONVENTION.id).subscribe({
      error: (error: ApiError) => (caught = error)
    });

    httpMock
      .expectOne(`${environment.apiUrl}/conventions/${CONVENTION.id}/generer`)
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(caught?.code).toBe('FORBIDDEN');
    expect(caught?.message).toContain('droits');
  });
});
