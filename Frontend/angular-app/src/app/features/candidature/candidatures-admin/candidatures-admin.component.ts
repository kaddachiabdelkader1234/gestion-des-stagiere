import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { StagiaireService } from '../../../core/services/stagiaire.service';
import { CandidatureService } from '../../../core/services/candidature.service';
import { UserService, UserSummary } from '../../../core/services/user.service';
import { ApiError } from '../../../core/http/api-error';
import { PagedResult } from '../../../core/models/paged-result.model';
import {
  Stagiaire,
  StagiaireQuery,
  STATUT_STAGIAIRE_BADGE,
  STATUT_STAGIAIRE_LABELS,
  STATUT_STAGIAIRE_VALUES,
  StatutStagiaire,
  TYPE_STAGE_LABELS,
  TYPE_STAGE_VALUES,
  TypeStage
} from '../../../core/models/stagiaire.model';

/**
 * Admin queue: review candidatures, then accept (assigning département + encadrant) or reject.
 *
 * Paging, filtering and search are all server-side — the table never receives the whole roster.
 */
@Component({
  selector: 'app-candidatures-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './candidatures-admin.component.html'
})
export class CandidaturesAdminComponent implements OnInit {
  page: PagedResult<Stagiaire> | null = null;
  chargement = true;
  erreur: string | null = null;
  succes: string | null = null;

  /** Defaults to the pending queue, which is what an admin opens this screen for. */
  filtres: StagiaireQuery = {
    page: 1,
    pageSize: 10,
    statut: 'EnAttente'
  };

  encadrants: UserSummary[] = [];

  /** Candidature being acted on, and which panel is open. */
  selection: Stagiaire | null = null;
  mode: 'accepter' | 'rejeter' | null = null;
  actionEnCours = false;

  // Accept form
  encadrantId: number | null = null;
  departement = '';
  dateDebut = '';
  dateFin = '';

  // Reject form
  motifRejet = '';

  erreursChamps: Record<string, string[]> = {};

  readonly statuts = STATUT_STAGIAIRE_VALUES;
  readonly statutLabels = STATUT_STAGIAIRE_LABELS;
  readonly statutBadges = STATUT_STAGIAIRE_BADGE;
  readonly typesStage = TYPE_STAGE_VALUES;
  readonly typeStageLabels = TYPE_STAGE_LABELS;

  // Preview modal
  previewOpen = false;
  previewTitle = '';
  previewUrl: string | null = null;
  previewSafeUrl: SafeResourceUrl | null = null;
  previewLoading = false;

  constructor(
    private stagiaireService: StagiaireService,
    private candidatureService: CandidatureService,
    private userService: UserService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.charger();
    this.chargerEncadrants();
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

  private chargerEncadrants(): void {
    this.userService.getTrainers().subscribe({
      next: encadrants => (this.encadrants = encadrants),
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  /** Any filter change resets to page 1 — staying on page 5 of a narrower result set shows nothing. */
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

  ouvrir(candidature: Stagiaire, mode: 'accepter' | 'rejeter'): void {
    this.selection = candidature;
    this.mode = mode;
    this.erreursChamps = {};
    this.erreur = null;

    // Prefill with what the candidate requested, so accepting unchanged is one click.
    this.departement = candidature.departement;
    this.dateDebut = candidature.dateDebut;
    this.dateFin = candidature.dateFin;
    this.encadrantId = null;
    this.motifRejet = '';
  }

  fermer(): void {
    this.selection = null;
    this.mode = null;
  }

  confirmerAcceptation(): void {
    if (!this.selection || this.actionEnCours) {
      return;
    }

    if (!this.encadrantId) {
      this.erreursChamps = { encadrantId: ['Sélectionnez un encadrant.'] };
      return;
    }

    const encadrant = this.encadrants.find(e => e.userId === Number(this.encadrantId));

    this.actionEnCours = true;
    this.erreursChamps = {};

    this.candidatureService
      .accepter(this.selection.id, {
        departement: this.departement,
        encadrantId: Number(this.encadrantId),
        encadrantNom: encadrant?.firstName ?? 'Encadrant',
        dateDebut: this.dateDebut,
        dateFin: this.dateFin
      })
      .subscribe({
        next: () => {
          this.actionEnCours = false;
          this.succes = 'Candidature acceptée. La convention va être générée.';
          this.fermer();
          this.charger();
        },
        error: (error: ApiError) => {
          this.actionEnCours = false;
          this.erreur = error.message;
          this.erreursChamps = error.fieldErrors ?? {};
        }
      });
  }

  confirmerRejet(): void {
    if (!this.selection || this.actionEnCours) {
      return;
    }

    this.actionEnCours = true;
    this.erreursChamps = {};

    this.candidatureService.rejeter(this.selection.id, { motifRejet: this.motifRejet }).subscribe({
      next: () => {
        this.actionEnCours = false;
        this.succes = 'Candidature rejetée.';
        this.fermer();
        this.charger();
      },
      error: (error: ApiError) => {
        this.actionEnCours = false;
        this.erreur = error.message;
        this.erreursChamps = error.fieldErrors ?? {};
      }
    });
  }

  telechargerCv(candidature: Stagiaire): void {
    this.candidatureService.telechargerCv(candidature.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = candidature.cvNomFichier ?? `cv-${candidature.nom}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  telechargerDocument(candidature: Stagiaire): void {
    this.candidatureService.telechargerDocument(candidature.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = candidature.documentNomFichier ?? `candidature-${candidature.nom}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  /** Open a preview modal for a file (CV or candidature document). */
  previewer(candidature: Stagiaire, type: 'cv' | 'document'): void {
    this.previewLoading = true;
    this.previewTitle = type === 'cv'
      ? `CV — ${candidature.prenom} ${candidature.nom}`
      : `Formulaire de candidature — ${candidature.prenom} ${candidature.nom}`;
    this.previewOpen = true;

    const call$ = type === 'cv'
      ? this.candidatureService.telechargerCv(candidature.id)
      : this.candidatureService.telechargerDocument(candidature.id);

    call$.subscribe({
      next: blob => {
        // Revoke previous URL to avoid memory leaks
        if (this.previewUrl) {
          URL.revokeObjectURL(this.previewUrl);
        }
        this.previewUrl = URL.createObjectURL(blob);
        this.previewSafeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.previewUrl);
        this.previewLoading = false;
      },
      error: (error: ApiError) => {
        this.previewLoading = false;
        this.previewOpen = false;
        this.erreur = error.message;
      }
    });
  }

  fermerPreview(): void {
    this.previewOpen = false;
    if (this.previewUrl) {
      URL.revokeObjectURL(this.previewUrl);
      this.previewUrl = null;
      this.previewSafeUrl = null;
    }
  }

  telechargerPreview(): void {
    if (!this.previewUrl) return;
    const link = document.createElement('a');
    link.href = this.previewUrl;
    link.download = this.previewTitle;
    link.click();
  }

  /** Only a pending candidature can be decided on. */
  estEnAttente(candidature: Stagiaire): boolean {
    return candidature.statut === 'EnAttente';
  }

  // Bound to <select> values, which arrive as strings — normalize to the union type or undefined.
  set statutFiltre(value: string) {
    this.filtres.statut = (value || undefined) as StatutStagiaire | undefined;
  }

  get statutFiltre(): string {
    return this.filtres.statut ?? '';
  }

  set typeStageFiltre(value: string) {
    this.filtres.typeStage = (value || undefined) as TypeStage | undefined;
  }

  get typeStageFiltre(): string {
    return this.filtres.typeStage ?? '';
  }
}
