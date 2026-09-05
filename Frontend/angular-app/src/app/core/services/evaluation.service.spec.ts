import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { EvaluationService } from './evaluation.service';
import { environment } from '../../../environments/environment';
import { Evaluation, EvaluationCreateRequest, StatutEvaluation, TypeEvaluation } from '../models/evaluation.model';

describe('EvaluationService', () => {
  let service: EvaluationService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/evaluations`;

  const mockEvaluation: Evaluation = {
    id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    stagiaireId: '11111111-2222-3333-4444-555555555555',
    utilisateurId: 10,
    encadrantId: 20,
    stagiaireNom: 'Ben Ali',
    stagiairePrenom: 'Amira',
    typeEvaluation: 'MiParcours' as TypeEvaluation,
    dateEvaluation: '2026-09-15',
    note: 15.5,
    commentaire: 'Bon travail durant cette première partie de stage.',
    statut: 'Soumise' as StatutEvaluation
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), EvaluationService]
    });
    service = TestBed.inject(EvaluationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should return a paged result', () => {
    const pagedResult = {
      items: [mockEvaluation],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.getAll({ page: 1, pageSize: 20 }).subscribe(result => {
      expect(result.items.length).toBe(1);
      expect(result.totalCount).toBe(1);
      expect(result.items[0].id).toBe(mockEvaluation.id);
      expect(result.items[0].note).toBe(15.5);
    });

    const req = httpMock.expectOne(`${baseUrl}?page=1&pageSize=20`);
    expect(req.request.method).toBe('GET');
    req.flush(pagedResult);
  });

  it('getAll should pass query parameters', () => {
    service
      .getAll({ stagiaireId: mockEvaluation.stagiaireId, statut: 'Soumise' })
      .subscribe();

    const req = httpMock.expectOne(
      r =>
        r.url === baseUrl &&
        r.params.get('stagiaireId') === mockEvaluation.stagiaireId &&
        r.params.get('statut') === 'Soumise'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasPreviousPage: false, hasNextPage: false });
  });

  it('getById should return a single evaluation', () => {
    service.getById(mockEvaluation.id).subscribe(evaluation => {
      expect(evaluation.id).toBe(mockEvaluation.id);
      expect(evaluation.typeEvaluation).toBe('MiParcours');
      expect(evaluation.statut).toBe('Soumise');
    });

    const req = httpMock.expectOne(`${baseUrl}/${mockEvaluation.id}`);
    expect(req.request.method).toBe('GET');
    req.flush(mockEvaluation);
  });

  it('creer should POST and return the created evaluation', () => {
    const body: EvaluationCreateRequest = {
      stagiaireId: mockEvaluation.stagiaireId,
      typeEvaluation: 'MiParcours',
      dateEvaluation: '2026-09-15',
      note: 15.5,
      commentaire: 'Bon travail durant cette première partie de stage.'
    };

    service.creer(body).subscribe(evaluation => {
      expect(evaluation.id).toBeDefined();
      expect(evaluation.statut).toBe('Soumise');
    });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(mockEvaluation);
  });

  it('modifier should PUT and return void (204)', () => {
    service
      .modifier(mockEvaluation.id, {
        typeEvaluation: 'Finale',
        dateEvaluation: '2026-12-15',
        note: 17,
        commentaire: 'Excellent travail.'
      })
      .subscribe(response => {
        expect(response).toBeNull();
      });

    const req = httpMock.expectOne(`${baseUrl}/${mockEvaluation.id}`);
    expect(req.request.method).toBe('PUT');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('valider should POST and return the validated evaluation', () => {
    service.valider(mockEvaluation.id).subscribe(evaluation => {
      expect(evaluation.statut).toBe('Validee');
    });

    const req = httpMock.expectOne(`${baseUrl}/${mockEvaluation.id}/valider`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...mockEvaluation, statut: 'Validee' });
  });

  it('supprimer should DELETE and return void (204)', () => {
    service.supprimer(mockEvaluation.id).subscribe(response => {
      expect(response).toBeNull();
    });

    const req = httpMock.expectOne(`${baseUrl}/${mockEvaluation.id}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('should propagate API errors', () => {
    service.getAll().subscribe({
      next: () => fail('should have errored'),
      error: error => {
        expect(error).toBeTruthy();
        expect(error.status).toBe(403);
        expect(error.code).toBe('FORBIDDEN');
      }
    });

    const req = httpMock.expectOne(baseUrl);
    req.flush(
      { error: 'Accès interdit', code: 'FORBIDDEN' },
      { status: 403, statusText: 'Forbidden' }
    );
  });
});
