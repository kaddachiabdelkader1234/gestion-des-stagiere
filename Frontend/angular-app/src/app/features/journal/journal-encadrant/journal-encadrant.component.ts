import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JournalService } from '../../../core/services/journal.service';
import { StagiaireService } from '../../../core/services/stagiaire.service';
import { ApiError } from '../../../core/http/api-error';
import { JOURNAL_LIMITS, JournalEntry } from '../../../core/models/journal.model';
import { Stagiaire } from '../../../core/models/stagiaire.model';

/**
 * The encadrant's view of their stagiaires' journals: read the weekly entries and leave a comment.
 *
 * The stagiaire list comes from `GET /api/v1/stagiaires`, which the server already scopes by role —
 * a TRAINER only ever gets their own assigned stagiaires, so there is no client-side filtering to
 * get wrong. An ADMIN reaching this screen sees everyone, which is intended.
 *
 * Writing entries is not offered here at all: the journal is the trainee's own account of their
 * work, and the API answers 403 to an encadrant who tries. The encadrant contributes comments.
 */
@Component({
  selector: 'app-journal-encadrant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './journal-encadrant.component.html'
})
export class JournalEncadrantComponent implements OnInit {
  stagiaires: Stagiaire[] = [];
  stagiaireId = '';

  entrees: JournalEntry[] = [];

  /**
   * Comment being typed, per entry id. Seeded with the saved comment so re-posting refines it
   * rather than starting from a blank box.
   */
  brouillons: Record<string, string> = {};

  chargement = true;
  chargementEntrees = false;
  /** Id of the entry whose comment is currently being saved, so only its button spins. */
  enregistrementId: string | null = null;

  erreur: string | null = null;
  succes: string | null = null;

  /** The encadrant's "to review" filter. */
  sansCommentaire = false;

  page = 1;
  totalCount = 0;
  totalPages = 0;
  readonly pageSize = 20;

  readonly limits = JOURNAL_LIMITS;

  constructor(
    private journalService: JournalService,
    private stagiaireService: StagiaireService
  ) {}

  ngOnInit(): void {
    this.chargerStagiaires();
  }

  chargerStagiaires(): void {
    this.chargement = true;
    this.erreur = null;

    // pageSize 100 is the server's clamp; an encadrant supervising more than that would need a
    // pager here, which no realistic roster requires.
    this.stagiaireService.getAll({ pageSize: 100 }).subscribe({
      next: resultat => {
        this.stagiaires = resultat.items;
        this.chargement = false;

        if (this.stagiaires.length > 0) {
          this.stagiaireId = this.stagiaires[0].id;
          this.chargerEntrees();
        }
      },
      error: (error: ApiError) => {
        this.erreur = error.message;
        this.chargement = false;
      }
    });
  }

  /** Called when the selected stagiaire or the filter changes — both restart at page 1. */
  changerSelection(): void {
    this.page = 1;
    this.succes = null;
    this.chargerEntrees();
  }

  chargerEntrees(): void {
    if (!this.stagiaireId) {
      return;
    }

    this.chargementEntrees = true;
    this.erreur = null;

    this.journalService
      .getAll(this.stagiaireId, {
        page: this.page,
        pageSize: this.pageSize,
        // Omitted entirely when false: sending `sansCommentaire=false` would be a filter that
        // matches nothing useful rather than "no filter".
        sansCommentaire: this.sansCommentaire ? true : undefined
      })
      .subscribe({
        next: resultat => {
          this.entrees = resultat.items;
          this.totalCount = resultat.totalCount;
          this.totalPages = resultat.totalPages;
          this.brouillons = {};

          for (const entree of resultat.items) {
            this.brouillons[entree.id] = entree.commentaireEncadrant ?? '';
          }

          this.chargementEntrees = false;
        },
        error: (error: ApiError) => {
          this.erreur = error.message;
          this.chargementEntrees = false;
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

  /** True once the draft holds enough text for the server to accept it. */
  commentaireValide(entryId: string): boolean {
    const texte = (this.brouillons[entryId] ?? '').trim();
    return texte.length >= this.limits.commentaireMin && texte.length <= this.limits.commentaireMax;
  }

  enregistrerCommentaire(entree: JournalEntry): void {
    if (!this.commentaireValide(entree.id) || this.enregistrementId) {
      return;
    }

    this.enregistrementId = entree.id;
    this.erreur = null;
    this.succes = null;

    this.journalService
      .commenter(this.stagiaireId, entree.id, {
        commentaire: this.brouillons[entree.id].trim()
      })
      .subscribe({
        next: misAJour => {
          this.enregistrementId = null;
          this.succes = `Retour enregistré pour la semaine du ${this.formatDate(entree.dateEntree)}.`;

          // Commenting freezes the entry, so `modifiable` flips — replace the row rather than
          // refetching the page, which would drop it out of a `sansCommentaire` filter mid-review.
          const index = this.entrees.findIndex(x => x.id === entree.id);

          if (index >= 0) {
            this.entrees[index] = misAJour;
            this.brouillons[misAJour.id] = misAJour.commentaireEncadrant ?? '';
          }
        },
        error: (error: ApiError) => {
          this.enregistrementId = null;
          this.erreur = error.message;
        }
      });
  }

  nomComplet(stagiaire: Stagiaire): string {
    return `${stagiaire.prenom} ${stagiaire.nom}`;
  }

  private formatDate(iso: string): string {
    const [annee, mois, jour] = iso.split('-');
    return `${jour}/${mois}/${annee}`;
  }
}
