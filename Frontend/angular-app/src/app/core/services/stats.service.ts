import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface StagiaireStats {
  totalStagiaires: number;
  pendingCandidatures: number;
  acceptedStagiaires: number;
  rejectedCandidatures: number;
  byDepartement: { departement: string; count: number }[];
  byTypeStage: { typeStage: string; count: number }[];
  byStatut: { statut: string; count: number }[];
}

export interface EvaluationStats {
  totalEvaluations: number;
  validatedEvaluations: number;
  averageScore: number;
  byType: { type: string; count: number }[];
}

export interface DashboardStats {
  stagiaires: StagiaireStats;
  evaluations: EvaluationStats;
}

@Injectable({ providedIn: 'root' })
export class StatsService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  /** Fetch both stagiaire and evaluation stats in parallel. */
  getDashboardStats(): Observable<DashboardStats> {
    return forkJoin({
      stagiaires: this.http.get<StagiaireStats>(`${this.apiUrl}/stagiaires/stats`),
      evaluations: this.http.get<EvaluationStats>(`${this.apiUrl}/evaluations/stats`)
    });
  }

  /** Download attestation PDF for a validated evaluation. */
  downloadAttestation(evaluationId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/evaluations/${evaluationId}/attestation`, {
      responseType: 'blob'
    });
  }
}
