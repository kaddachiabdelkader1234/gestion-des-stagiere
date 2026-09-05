import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { JournalService } from './journal.service';
import { JournalEntry } from '../models/journal.model';
import { PagedResult } from '../models/paged-result.model';
import { ApiError } from '../http/api-error';
import { environment } from '../../../environments/environment';

const STAGIAIRE_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const ENTRY_ID = '7b2c9d10-2222-4a5b-8c6d-1e3f4a5b6c7d';

/** Shaped exactly like JournalEntryReadDto. */
const ENTRY: JournalEntry = {
  id: ENTRY_ID,
  stagiaireId: STAGIAIRE_ID,
  dateEntree: '2026-08-03',
  texte: 'Semaine 1 : prise en main du projet et de la base de code.',
  commentaireEncadrant: null,
  commentaireParId: null,
  dateCommentaire: null,
  dateCreation: '2026-08-04T09:15:00Z',
  dateModification: null,
  estCommentee: false,
  modifiable: true
};

const PAGE: PagedResult<JournalEntry> = {
  items: [ENTRY],
  totalCount: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false
};

const BASE = `${environment.apiUrl}/stagiaires/${STAGIAIRE_ID}/journal`;

describe('JournalService', () => {
  let service: JournalService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(JournalService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('nests the journal under its stagiaire on the versioned route', () => {
    service.getAll(STAGIAIRE_ID).subscribe();

    const req = httpMock.expectOne(BASE);
    expect(req.request.url).toBe(
      `http://localhost:18080/api/v1/stagiaires/${STAGIAIRE_ID}/journal`
    );
    expect(req.request.method).toBe('GET');
    req.flush(PAGE);
  });

  it('returns the paged envelope, not a bare array', () => {
    let received: PagedResult<JournalEntry> | undefined;
    service.getAll(STAGIAIRE_ID).subscribe(page => (received = page));

    httpMock.expectOne(BASE).flush(PAGE);

    expect(received?.items.length).toBe(1);
    expect(received?.totalCount).toBe(1);
    expect(received?.items[0].modifiable).toBeTrue();
  });

  it('sends only the filters that have a value', () => {
    service.getAll(STAGIAIRE_ID, { page: 2, sansCommentaire: true, du: '' }).subscribe();

    const req = httpMock.expectOne(r => r.url === BASE);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('sansCommentaire')).toBe('true');
    // An empty date must be dropped — the server would reject it as an invalid DateOnly.
    expect(req.request.params.has('du')).toBeFalse();
    req.flush(PAGE);
  });

  it('creates an entry and returns it', () => {
    let received: JournalEntry | undefined;
    service
      .creer(STAGIAIRE_ID, { dateEntree: '2026-08-03', texte: ENTRY.texte })
      .subscribe(entry => (received = entry));

    const req = httpMock.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ dateEntree: '2026-08-03', texte: ENTRY.texte });
    req.flush(ENTRY);

    expect(received?.id).toBe(ENTRY_ID);
    expect(received?.estCommentee).toBeFalse();
  });

  it('edits an entry with a PUT carrying neither stagiaireId nor comment fields', () => {
    service
      .modifier(STAGIAIRE_ID, ENTRY_ID, { dateEntree: '2026-08-03', texte: 'Texte corrigé.' })
      .subscribe();

    const req = httpMock.expectOne(`${BASE}/${ENTRY_ID}`);
    expect(req.request.method).toBe('PUT');
    // A replacement PUT that carried ownership could null it out and orphan the entry.
    expect(Object.keys(req.request.body).sort()).toEqual(['dateEntree', 'texte']);
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('posts a comment and returns the frozen entry', () => {
    let received: JournalEntry | undefined;
    service
      .commenter(STAGIAIRE_ID, ENTRY_ID, { commentaire: 'Bon démarrage.' })
      .subscribe(entry => (received = entry));

    const req = httpMock.expectOne(`${BASE}/${ENTRY_ID}/commentaire`);
    expect(req.request.method).toBe('POST');
    req.flush({
      ...ENTRY,
      commentaireEncadrant: 'Bon démarrage.',
      commentaireParId: 12,
      dateCommentaire: '2026-08-06T10:00:00Z',
      estCommentee: true,
      modifiable: false
    });

    expect(received?.estCommentee).toBeTrue();
    // Commenting is what freezes the stagiaire's text — the UI hides "modifier" on this flag.
    expect(received?.modifiable).toBeFalse();
  });

  it('deletes an entry', () => {
    service.supprimer(STAGIAIRE_ID, ENTRY_ID).subscribe();

    const req = httpMock.expectOne(`${BASE}/${ENTRY_ID}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('surfaces a 409 on a duplicate week instead of swallowing it', () => {
    let caught: ApiError | undefined;
    let emitted = false;

    service.creer(STAGIAIRE_ID, { dateEntree: '2026-08-03', texte: ENTRY.texte }).subscribe({
      next: () => (emitted = true),
      error: (error: ApiError) => (caught = error)
    });

    httpMock.expectOne(BASE).flush(
      { error: 'Une entrée de journal existe déjà pour la semaine du 03/08/2026.', code: 'CONFLICT' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(emitted).toBeFalse();
    expect(caught?.status).toBe(409);
    expect(caught?.code).toBe('CONFLICT');
  });

  it('surfaces a 409 when the entry has already been commented', () => {
    let caught: ApiError | undefined;

    service
      .modifier(STAGIAIRE_ID, ENTRY_ID, { dateEntree: '2026-08-03', texte: 'Trop tard.' })
      .subscribe({ error: (error: ApiError) => (caught = error) });

    httpMock.expectOne(`${BASE}/${ENTRY_ID}`).flush(
      { error: 'Cette entrée a été commentée par l\'encadrant.', code: 'CONFLICT' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(caught?.status).toBe(409);
  });

  it('reports a 404 for an out-of-scope stagiaire, which means "not yours"', () => {
    let caught: ApiError | undefined;

    // The service returns 404 rather than 403 for a stagiaire the caller may not see: a 403 would
    // confirm the id exists.
    service.getAll(STAGIAIRE_ID).subscribe({ error: (error: ApiError) => (caught = error) });

    httpMock.expectOne(BASE).flush(
      { error: "Le stagiaire est introuvable.", code: 'NOT_FOUND' },
      { status: 404, statusText: 'Not Found' }
    );

    expect(caught?.status).toBe(404);
    expect(caught?.code).toBe('NOT_FOUND');
  });

  it('reports a 403 when an encadrant tries to write an entry', () => {
    let caught: ApiError | undefined;

    // 403 not 404 here: the encadrant legitimately sees this stagiaire, they just do not author
    // the journal — they contribute through commenter().
    service.creer(STAGIAIRE_ID, { dateEntree: '2026-08-03', texte: ENTRY.texte }).subscribe({
      error: (error: ApiError) => (caught = error)
    });

    httpMock.expectOne(BASE).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(caught?.code).toBe('FORBIDDEN');
    expect(caught?.message).toContain('droits');
  });

  it('surfaces per-field validation errors from a 400', () => {
    let caught: ApiError | undefined;

    service.creer(STAGIAIRE_ID, { dateEntree: '2026-08-03', texte: 'court' }).subscribe({
      error: (error: ApiError) => (caught = error)
    });

    httpMock.expectOne(BASE).flush(
      {
        error: 'Un ou plusieurs champs sont invalides.',
        code: 'VALIDATION_ERROR',
        errors: { texte: ['Le texte doit comporter au moins 10 caractères.'] }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(caught?.status).toBe(400);
    expect(caught?.fieldErrors?.['texte'].length).toBe(1);
  });
});
