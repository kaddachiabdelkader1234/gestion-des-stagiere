import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConventionService } from '../../../core/services/convention.service';
import { ApiError } from '../../../core/http/api-error';
import { PagedResult } from '../../../core/models/paged-result.model';
import {
  Convention,
  ConventionQuery,
  STATUT_SIGNATURE_BADGE,
  STATUT_SIGNATURE_LABELS,
  STATUT_SIGNATURE_VALUES,
  StatutSignature
} from '../../../core/models/convention.model';

/**
 * Admin screen for conventions: generate the PDF, download it, mark it signed.
 *
 * Rows appear here on their own — a draft convention is created by Convention.Service's
 * `CandidatureAcceptedConsumer` when an admin accepts a candidature, so there is no "create"
 * action. An empty table means no candidature has been accepted yet.
 *
 * Paging and filtering are server-side.
 */
@Component({
  selector: 'app-conventions-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './conventions-admin.component.html'
})
export class ConventionsAdminComponent implements OnInit {
  page: PagedResult<Convention> | null = null;
  chargement = true;
  erreur: string | null = null;
  succes: string | null = null;

  filtres: ConventionQuery = {
    page: 1,
    pageSize: 10
  };

  /** Id of the convention whose action is running, so only that row's buttons spin. */
  actionEnCoursId: string | null = null;

  /** Convention queued for the "mark as signed" confirmation. */
  aSigner: Convention | null = null;

  readonly statuts = STATUT_SIGNATURE_VALUES;
  readonly statutLabels = STATUT_SIGNATURE_LABELS;
  readonly statutBadges = STATUT_SIGNATURE_BADGE;

  constructor(private conventionService: ConventionService) {}

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.chargement = true;
    this.erreur = null;

    this.conventionService.getAll(this.filtres).subscribe({
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

  /**
   * Generates (or regenerates) the PDF. The server replaces any previous file, so this is safe to
   * repeat after the stagiaire's details change.
   */
  generer(convention: Convention): void {
    if (this.actionEnCoursId) {
      return;
    }

    this.actionEnCoursId = convention.id;
    this.erreur = null;
    this.succes = null;

    this.conventionService.generer(convention.id).subscribe({
      next: maj => {
        this.actionEnCoursId = null;
        this.succes = `Convention de ${maj.stagiairePrenom} ${maj.stagiaireNom} générée.`;
        this.remplacer(maj);
      },
      error: (error: ApiError) => {
        this.actionEnCoursId = null;
        this.erreur = error.message;
      }
    });
  }

  telechargerPdf(convention: Convention): void {
    this.erreur = null;

    this.conventionService.telechargerPdf(convention.id).subscribe({
      next: blob => this.enregistrerBlob(
        blob,
        `convention-${convention.stagiairePrenom}-${convention.stagiaireNom}.pdf`
      ),
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  demanderSignature(convention: Convention): void {
    this.aSigner = convention;
    this.erreur = null;
    this.succes = null;
  }

  annulerSignature(): void {
    this.aSigner = null;
  }

  confirmerSignature(): void {
    if (!this.aSigner || this.actionEnCoursId) {
      return;
    }

    const cible = this.aSigner;
    this.actionEnCoursId = cible.id;

    this.conventionService.signer(cible.id).subscribe({
      next: maj => {
        this.actionEnCoursId = null;
        this.aSigner = null;
        this.succes = `Convention de ${maj.stagiairePrenom} ${maj.stagiaireNom} marquée comme signée.`;
        this.remplacer(maj);
      },
      error: (error: ApiError) => {
        this.actionEnCoursId = null;
        this.aSigner = null;
        this.erreur = error.message;
      }
    });
  }

  estSignee(convention: Convention): boolean {
    return convention.statutSignature === 'Signee';
  }

  /**
   * Patches the row in place from the endpoint's response rather than refetching the page: a reload
   * would lose the admin's scroll position and re-run the query for a single changed field.
   */
  private remplacer(maj: Convention): void {
    if (!this.page) {
      return;
    }

    this.page = {
      ...this.page,
      items: this.page.items.map(c => (c.id === maj.id ? maj : c))
    };
  }

  private enregistrerBlob(blob: Blob, nomFichier: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = nomFichier;
    link.click();
    URL.revokeObjectURL(url);
  }

  // Bound to a <select>, whose value arrives as a string — normalize to the union type or undefined.
  set statutFiltre(value: string) {
    this.filtres.statutSignature = (value || undefined) as StatutSignature | undefined;
  }

  get statutFiltre(): string {
    return this.filtres.statutSignature ?? '';
  }
}
