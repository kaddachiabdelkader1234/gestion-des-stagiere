import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EvaluationService } from '../../../core/services/evaluation.service';
import { CandidatureService } from '../../../core/services/candidature.service';
import { ApiError } from '../../../core/http/api-error';
import {
  Evaluation,
  STATUT_EVALUATION_BADGE,
  STATUT_EVALUATION_LABELS,
  TYPE_EVALUATION_BADGE,
  TYPE_EVALUATION_LABELS
} from '../../../core/models/evaluation.model';
import { Stagiaire } from '../../../core/models/stagiaire.model';

/**
 * The stagiaire's own evaluation result view: read-only, showing their evaluation(s) once
 * submitted by the encadrant.
 *
 * Resolved in two hops, like `mon-journal`:
 *   GET /api/v1/candidatures/moi                       → the caller's own Stagiaire
 *   GET /api/v1/evaluations?stagiaireId={that id}
 *
 * Evaluations only appear once submitted by the encadrant, so an accepted candidature with no
 * evaluation yet gets its own explanatory panel.
 */
@Component({
  selector: 'app-mon-evaluation',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './mon-evaluation.component.html'
})
export class MonEvaluationComponent implements OnInit {
  candidature: Stagiaire | null = null;
  evaluations: Evaluation[] = [];

  chargement = true;
  erreur: string | null = null;

  /** True when the learner has not applied yet. */
  aucuneCandidature = false;

  readonly typeLabels = TYPE_EVALUATION_LABELS;
  readonly typeBadge = TYPE_EVALUATION_BADGE;
  readonly statutLabels = STATUT_EVALUATION_LABELS;
  readonly statutBadge = STATUT_EVALUATION_BADGE;

  constructor(
    private evaluationService: EvaluationService,
    private candidatureService: CandidatureService
  ) {}

  ngOnInit(): void {
    this.charger();
  }

  /** True once the candidature is accepted, i.e. there is a stage to evaluate. */
  get stageActif(): boolean {
    return (
      this.candidature !== null &&
      this.candidature.statut !== 'EnAttente' &&
      this.candidature.statut !== 'Rejetee'
    );
  }

  charger(): void {
    this.chargement = true;
    this.erreur = null;
    this.aucuneCandidature = false;

    this.candidatureService.maCandidature().subscribe({
      next: candidature => {
        this.candidature = candidature;

        if (!this.stageActif) {
          this.chargement = false;
          return;
        }

        this.chargerEvaluations();
      },
      error: (error: ApiError) => {
        this.chargement = false;

        if (error.status === 404) {
          this.aucuneCandidature = true;
          return;
        }

        this.erreur = error.message;
      }
    });
  }

  chargerEvaluations(): void {
    if (!this.candidature) {
      return;
    }

    this.chargement = true;

    this.evaluationService
      .getAll({ stagiaireId: this.candidature.id })
      .subscribe({
        next: resultat => {
          this.evaluations = resultat.items;
          this.chargement = false;
        },
        error: (error: ApiError) => {
          this.erreur = error.message;
          this.chargement = false;
        }
      });
  }

  /** Formats a note as "X.X/20". */
  formatNote(note: number): string {
    return `${note}/20`;
  }

  /** Computes the average note from all validated evaluations. */
  get noteMoyenne(): number | null {
    const validees = this.evaluations.filter(e => e.statut === 'Validee');
    if (validees.length === 0) {
      return null;
    }
    const total = validees.reduce((sum, e) => sum + e.note, 0);
    return Math.round((total / validees.length) * 10) / 10;
  }
}
