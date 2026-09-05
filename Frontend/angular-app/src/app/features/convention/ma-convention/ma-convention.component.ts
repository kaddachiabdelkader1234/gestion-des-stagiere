import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ConventionService } from '../../../core/services/convention.service';
import { CandidatureService } from '../../../core/services/candidature.service';
import { ApiError } from '../../../core/http/api-error';
import {
  Convention,
  STATUT_SIGNATURE_BADGE,
  STATUT_SIGNATURE_LABELS
} from '../../../core/models/convention.model';
import { Stagiaire } from '../../../core/models/stagiaire.model';

/**
 * The stagiaire's own convention: status and PDF download.
 *
 * Resolved in two hops, because a convention is keyed by the stagiaire record's Guid and the JWT
 * only carries the auth-service user id:
 *   GET /api/v1/candidatures/moi  → the caller's own Stagiaire (404 if they never applied)
 *   GET /api/v1/conventions?stagiaireId=<that id>
 *
 * There is nothing to do here until an admin has accepted the candidature — the convention is
 * created by Convention.Service's consumer at that moment — so each intermediate state gets its own
 * explanatory panel rather than an empty screen.
 */
@Component({
  selector: 'app-ma-convention',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './ma-convention.component.html'
})
export class MaConventionComponent implements OnInit {
  candidature: Stagiaire | null = null;
  convention: Convention | null = null;

  chargement = true;
  telechargementEnCours = false;
  erreur: string | null = null;

  /** True when the learner has simply not applied yet — a normal state, not a failure. */
  aucuneCandidature = false;

  readonly statutLabels = STATUT_SIGNATURE_LABELS;
  readonly statutBadges = STATUT_SIGNATURE_BADGE;

  constructor(
    private conventionService: ConventionService,
    private candidatureService: CandidatureService
  ) {}

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.chargement = true;
    this.erreur = null;
    this.aucuneCandidature = false;

    this.candidatureService.maCandidature().subscribe({
      next: candidature => {
        this.candidature = candidature;

        // Only an accepted candidature has a convention; skip the second call otherwise.
        if (candidature.statut === 'EnAttente' || candidature.statut === 'Rejetee') {
          this.chargement = false;
          return;
        }

        this.chargerConvention(candidature.id);
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

  private chargerConvention(stagiaireId: string): void {
    this.conventionService.getAll({ stagiaireId, pageSize: 1 }).subscribe({
      next: page => {
        // Filtering by stagiaireId yields at most one row; absent means the consumer has not run yet.
        this.convention = page.items[0] ?? null;
        this.chargement = false;
      },
      error: (error: ApiError) => {
        this.erreur = error.message;
        this.chargement = false;
      }
    });
  }

  telecharger(): void {
    if (!this.convention || this.telechargementEnCours) {
      return;
    }

    const cible = this.convention;
    this.telechargementEnCours = true;
    this.erreur = null;

    this.conventionService.telechargerPdf(cible.id).subscribe({
      next: blob => {
        this.telechargementEnCours = false;

        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `convention-${cible.stagiairePrenom}-${cible.stagiaireNom}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => {
        this.telechargementEnCours = false;
        this.erreur = error.message;
      }
    });
  }
}
