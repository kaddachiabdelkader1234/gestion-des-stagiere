import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EvaluationService } from '../../../core/services/evaluation.service';
import { StagiaireService } from '../../../core/services/stagiaire.service';
import { ApiError } from '../../../core/http/api-error';
import {
  EVALUATION_LIMITS,
  Evaluation,
  STATUT_EVALUATION_BADGE,
  STATUT_EVALUATION_LABELS,
  STATUT_EVALUATION_VALUES,
  StatutEvaluation,
  TYPE_EVALUATION_BADGE,
  TYPE_EVALUATION_LABELS,
  TYPE_EVALUATION_VALUES,
  TypeEvaluation
} from '../../../core/models/evaluation.model';
import { Stagiaire } from '../../../core/models/stagiaire.model';

/**
 * The encadrant's view: list evaluations for assigned stagiaires, create new ones, edit before
 * validation, and validate (admin only).
 *
 * The stagiaire list comes from `GET /api/v1/stagiaires`, which the server already scopes by role —
 * a TRAINER only ever gets their own assigned stagiaires, so there is no client-side filtering to
 * get wrong.
 */
@Component({
  selector: 'app-evaluation-encadrant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './evaluation-encadrant.component.html'
})
export class EvaluationEncadrantComponent implements OnInit {
  stagiaires: Stagiaire[] = [];
  evaluations: Evaluation[] = [];

  chargement = true;
  chargementEvaluations = false;
  enregistrement = false;
  erreur: string | null = null;
  succes: string | null = null;

  /** Stagiaire selected for creating a new evaluation. */
  stagiaireSelectionne: Stagiaire | null = null;

  /** Evaluation being edited, or null when composing a new one. */
  editionId: string | null = null;

  /** Form fields. */
  typeEvaluation: TypeEvaluation = 'MiParcours';
  dateEvaluation = '';
  note: number | null = null;
  commentaire = '';

  /** Filters. */
  filtreStatut: StatutEvaluation | '' = '';
  filtreType: TypeEvaluation | '' = '';

  page = 1;
  totalCount = 0;
  totalPages = 0;
  readonly pageSize = 20;

  readonly limits = EVALUATION_LIMITS;
  readonly typeValues = TYPE_EVALUATION_VALUES;
  readonly typeLabels = TYPE_EVALUATION_LABELS;
  readonly typeBadge = TYPE_EVALUATION_BADGE;
  readonly statutValues = STATUT_EVALUATION_VALUES;
  readonly statutLabels = STATUT_EVALUATION_LABELS;
  readonly statutBadge = STATUT_EVALUATION_BADGE;

  constructor(
    private evaluationService: EvaluationService,
    private stagiaireService: StagiaireService
  ) {}

  ngOnInit(): void {
    this.chargerStagiaires();
  }

  chargerStagiaires(): void {
    this.chargement = true;
    this.erreur = null;

    this.stagiaireService.getAll({ pageSize: 100 }).subscribe({
      next: resultat => {
        this.stagiaires = resultat.items;
        this.chargement = false;
        this.chargerEvaluations();
      },
      error: (error: ApiError) => {
        this.erreur = error.message;
        this.chargement = false;
      }
    });
  }

  chargerEvaluations(): void {
    this.chargementEvaluations = true;
    this.erreur = null;

    const query: Record<string, unknown> = {
      page: this.page,
      pageSize: this.pageSize
    };

    if (this.filtreStatut) {
      query['statut'] = this.filtreStatut;
    }
    if (this.filtreType) {
      query['typeEvaluation'] = this.filtreType;
    }

    this.evaluationService.getAll(query as any).subscribe({
      next: resultat => {
        this.evaluations = resultat.items;
        this.totalCount = resultat.totalCount;
        this.totalPages = resultat.totalPages;
        this.chargementEvaluations = false;
      },
      error: (error: ApiError) => {
        this.erreur = error.message;
        this.chargementEvaluations = false;
      }
    });
  }

  changerFiltre(): void {
    this.page = 1;
    this.succes = null;
    this.chargerEvaluations();
  }

  allerPage(page: number): void {
    if (page < 1 || (this.totalPages > 0 && page > this.totalPages)) {
      return;
    }
    this.page = page;
    this.chargerEvaluations();
  }

  /** Opens the form to create a new evaluation for the selected stagiaire. */
  ouvrirFormulaire(stagiaire?: Stagiaire): void {
    this.editionId = null;
    this.stagiaireSelectionne = stagiaire ?? this.stagiaires[0] ?? null;
    this.typeEvaluation = 'MiParcours';
    this.dateEvaluation = new Date().toISOString().slice(0, 10);
    this.note = null;
    this.commentaire = '';
    this.erreur = null;
    this.succes = null;
  }

  /** Loads an existing evaluation into the form for editing. */
  modifier(evalObj: Evaluation): void {
    this.editionId = evalObj.id;
    this.stagiaireSelectionne = this.stagiaires.find(s => s.id === evalObj.stagiaireId) ?? null;
    this.typeEvaluation = evalObj.typeEvaluation;
    this.dateEvaluation = evalObj.dateEvaluation;
    this.note = evalObj.note;
    this.commentaire = evalObj.commentaire;
    this.erreur = null;
    this.succes = null;
  }

  annulerFormulaire(): void {
    this.editionId = null;
    this.stagiaireSelectionne = null;
  }

  /** True when the form fields pass the server's validation rules. */
  get formulaireValide(): boolean {
    return (
      this.stagiaireSelectionne !== null &&
      this.dateEvaluation !== '' &&
      this.note !== null &&
      this.note >= this.limits.noteMin &&
      this.note <= this.limits.noteMax &&
      this.commentaire.trim().length >= this.limits.commentaireMin &&
      this.commentaire.trim().length <= this.limits.commentaireMax
    );
  }

  enregistrer(): void {
    if (!this.formulaireValide || this.enregistrement || !this.stagiaireSelectionne) {
      return;
    }

    this.enregistrement = true;
    this.erreur = null;
    this.succes = null;

    if (this.editionId) {
      this.evaluationService
        .modifier(this.editionId, {
          typeEvaluation: this.typeEvaluation,
          dateEvaluation: this.dateEvaluation,
          note: this.note!,
          commentaire: this.commentaire.trim()
        })
        .subscribe({
          next: () => {
            this.enregistrement = false;
            this.succes = 'Évaluation mise à jour.';
            this.annulerFormulaire();
            this.chargerEvaluations();
          },
          error: (error: ApiError) => {
            this.enregistrement = false;
            this.erreur = error.message;
          }
        });
    } else {
      this.evaluationService
        .creer({
          stagiaireId: this.stagiaireSelectionne.id,
          typeEvaluation: this.typeEvaluation,
          dateEvaluation: this.dateEvaluation,
          note: this.note!,
          commentaire: this.commentaire.trim()
        })
        .subscribe({
          next: () => {
            this.enregistrement = false;
            this.succes = 'Évaluation enregistrée.';
            this.annulerFormulaire();
            this.chargerEvaluations();
          },
          error: (error: ApiError) => {
            this.enregistrement = false;
            this.erreur = error.message;
          }
        });
    }
  }

  valider(evalObj: Evaluation): void {
    if (this.enregistrement) {
      return;
    }

    this.enregistrement = true;
    this.erreur = null;

    this.evaluationService.valider(evalObj.id).subscribe({
      next: () => {
        this.enregistrement = false;
        this.succes = `Évaluation de ${evalObj.stagiairePrenom} ${evalObj.stagiaireNom} validée.`;
        this.chargerEvaluations();
      },
      error: (error: ApiError) => {
        this.enregistrement = false;
        this.erreur = error.message;
      }
    });
  }

  supprimer(evalObj: Evaluation): void {
    if (!confirm(`Supprimer l'évaluation de ${evalObj.stagiairePrenom} ${evalObj.stagiaireNom} ?`)) {
      return;
    }

    this.enregistrement = true;
    this.erreur = null;

    this.evaluationService.supprimer(evalObj.id).subscribe({
      next: () => {
        this.enregistrement = false;
        this.succes = 'Évaluation supprimée.';
        this.chargerEvaluations();
      },
      error: (error: ApiError) => {
        this.enregistrement = false;
        this.erreur = error.message;
      }
    });
  }

  /** Formats a note as "X.X/20". */
  formatNote(note: number): string {
    return `${note}/20`;
  }

  nomComplet(stagiaire: Stagiaire): string {
    return `${stagiaire.prenom} ${stagiaire.nom}`;
  }

  nomCompletEvaluation(evalObj: { stagiairePrenom: string; stagiaireNom: string }): string {
    return `${evalObj.stagiairePrenom} ${evalObj.stagiaireNom}`;
  }
}
