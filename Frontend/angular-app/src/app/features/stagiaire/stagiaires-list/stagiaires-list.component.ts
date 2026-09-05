import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { StagiaireService } from '../../../core/services/stagiaire.service';
import { CandidatureService } from '../../../core/services/candidature.service';
import { ApiError } from '../../../core/http/api-error';
import { PagedResult } from '../../../core/models/paged-result.model';
import {
  Stagiaire,
  StagiaireQuery,
  STATUT_STAGIAIRE_BADGE,
  STATUT_STAGIAIRE_LABELS,
  STATUT_STAGIAIRE_VALUES,
  StatutStagiaire,
  TYPE_STAGE_LABELS
} from '../../../core/models/stagiaire.model';

/**
 * Read-only, paginated roster.
 *
 * Serves both `/dashboard/stagiaires` (admin) and `/dashboard/mes-stagiaires` (encadrant) from one
 * component: the API already scopes results by role — an admin gets everyone, a trainer only their
 * assigned stagiaires — so a second component would differ only in its heading.
 */
@Component({
  selector: 'app-stagiaires-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './stagiaires-list.component.html'
})
export class StagiairesListComponent implements OnInit {
  page: PagedResult<Stagiaire> | null = null;
  chargement = true;
  erreur: string | null = null;

  filtres: StagiaireQuery = { page: 1, pageSize: 10 };

  readonly statuts = STATUT_STAGIAIRE_VALUES;
  readonly statutLabels = STATUT_STAGIAIRE_LABELS;
  readonly statutBadges = STATUT_STAGIAIRE_BADGE;
  readonly typeStageLabels = TYPE_STAGE_LABELS;

  constructor(
    private stagiaireService: StagiaireService,
    private candidatureService: CandidatureService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.charger();
  }

  get estEncadrant(): boolean {
    return this.authService.hasRole('TRAINER') && !this.authService.isAdmin();
  }

  get titre(): string {
    return this.estEncadrant ? 'Mes stagiaires' : 'Stagiaires';
  }

  get sousTitre(): string {
    return this.estEncadrant
      ? 'Les stagiaires dont vous êtes l\'encadrant'
      : 'Tous les stagiaires enregistrés';
  }

  charger(): void {
    this.chargement = true;
    this.erreur = null;

    this.stagiaireService.getAll(this.filtres).subscribe({
      next: page => {
        this.page = page;
        this.chargement = false;
      },
      error: (error: ApiError) => {
        this.erreur = error.message;
        this.chargement = false;
      }
    });
  }

  appliquerFiltres(): void {
    this.filtres.page = 1;
    this.charger();
  }

  changerPage(delta: number): void {
    const cible = (this.filtres.page ?? 1) + delta;

    if (cible < 1 || (this.page && cible > this.page.totalPages)) {
      return;
    }

    this.filtres.page = cible;
    this.charger();
  }

  telechargerCv(stagiaire: Stagiaire): void {
    this.candidatureService.telechargerCv(stagiaire.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = stagiaire.cvNomFichier ?? `cv-${stagiaire.nom}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  set statutFiltre(value: string) {
    this.filtres.statut = (value || undefined) as StatutStagiaire | undefined;
  }

  get statutFiltre(): string {
    return this.filtres.statut ?? '';
  }
}
