import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { JournalService } from '../../../core/services/journal.service';
import { CandidatureService } from '../../../core/services/candidature.service';
import { ApiError } from '../../../core/http/api-error';
import { JOURNAL_LIMITS, JournalEntry } from '../../../core/models/journal.model';
import { Stagiaire } from '../../../core/models/stagiaire.model';

/**
 * The stagiaire's own journal de bord: write a weekly entry, correct it while it is still
 * uncommented, and read the encadrant's feedback.
 *
 * Resolved in two hops, because the journal is keyed by the stagiaire record's Guid while the JWT
 * only carries the auth-service user id:
 *   GET /api/v1/candidatures/moi                       → the caller's own Stagiaire
 *   GET /api/v1/stagiaires/{that id}/journal
 *
 * The journal only opens once a stage exists, so a pending or rejected candidature gets its own
 * explanatory panel rather than a form that would 409 on submit.
 */
@Component({
  selector: 'app-mon-journal',
  standalone: true,
  imports: [CommonModule, RouterLink, ReactiveFormsModule],
  templateUrl: './mon-journal.component.html'
})
export class MonJournalComponent implements OnInit {
  candidature: Stagiaire | null = null;
  entrees: JournalEntry[] = [];

  chargement = true;
  enregistrement = false;
  erreur: string | null = null;
  succes: string | null = null;

  /** True when the learner has simply not applied yet — a normal state, not a failure. */
  aucuneCandidature = false;

  /** Id of the entry being edited, or null when the form is composing a new one. */
  editionId: string | null = null;

  form: FormGroup;

  page = 1;
  totalCount = 0;
  totalPages = 0;
  readonly pageSize = 20;

  readonly limits = JOURNAL_LIMITS;

  /**
   * Upper bound for the date picker, mirroring the server's "not more than 7 days ahead" rule so
   * the calendar cannot offer a date the API would reject.
   */
  readonly dateMax = MonJournalComponent.isoDate(JOURNAL_LIMITS.joursAvanceMax);

  constructor(
    private journalService: JournalService,
    private candidatureService: CandidatureService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      dateEntree: ['', [Validators.required]],
      texte: [
        '',
        [
          Validators.required,
          Validators.minLength(JOURNAL_LIMITS.texteMin),
          Validators.maxLength(JOURNAL_LIMITS.texteMax)
        ]
      ]
    });
  }

  ngOnInit(): void {
    this.charger();
  }

  /** True once the candidature is accepted, i.e. there is a stage to journal about. */
  get journalOuvert(): boolean {
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

        if (!this.journalOuvert) {
          this.chargement = false;
          return;
        }

        this.chargerEntrees();
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

  chargerEntrees(): void {
    if (!this.candidature) {
      return;
    }

    this.chargement = true;

    this.journalService
      .getAll(this.candidature.id, { page: this.page, pageSize: this.pageSize })
      .subscribe({
        next: resultat => {
          this.entrees = resultat.items;
          this.totalCount = resultat.totalCount;
          this.totalPages = resultat.totalPages;
          this.chargement = false;
        },
        error: (error: ApiError) => {
          this.erreur = error.message;
          this.chargement = false;
        }
      });
  }

  allerPage(page: number): void {
    if (page < 1 || (this.totalPages > 0 && page > this.totalPages)) {
      return;
    }

    this.page = page;
    this.chargerEntrees();
  }

  /** Loads an existing entry into the form. Only offered while `modifiable`. */
  modifier(entree: JournalEntry): void {
    this.editionId = entree.id;
    this.erreur = null;
    this.succes = null;
    this.form.patchValue({ dateEntree: entree.dateEntree, texte: entree.texte });
  }

  annulerEdition(): void {
    this.editionId = null;
    this.form.reset();
  }

  enregistrer(): void {
    if (!this.candidature || this.form.invalid || this.enregistrement) {
      this.form.markAllAsTouched();
      return;
    }

    const stagiaireId = this.candidature.id;
    const body = {
      dateEntree: this.form.value.dateEntree as string,
      texte: (this.form.value.texte as string).trim()
    };

    this.enregistrement = true;
    this.erreur = null;
    this.succes = null;

    // PUT answers 204 with no body, so both paths refetch rather than patching the row locally.
    // Typed as unknown because the two calls emit different shapes and neither value is used.
    const requete: Observable<unknown> = this.editionId
      ? this.journalService.modifier(stagiaireId, this.editionId, body)
      : this.journalService.creer(stagiaireId, body);

    requete.subscribe({
      next: () => {
        this.enregistrement = false;
        this.succes = this.editionId ? 'Entrée mise à jour.' : 'Entrée ajoutée à votre journal.';
        this.editionId = null;
        this.form.reset();
        this.chargerEntrees();
      },
      error: (error: ApiError) => {
        this.enregistrement = false;
        this.erreur = error.message;
      }
    });
  }

  /** Today plus `joursEnAvance`, as "yyyy-MM-dd" for a native date input. */
  private static isoDate(joursEnAvance = 0): string {
    const date = new Date();
    date.setDate(date.getDate() + joursEnAvance);
    return date.toISOString().slice(0, 10);
  }
}
