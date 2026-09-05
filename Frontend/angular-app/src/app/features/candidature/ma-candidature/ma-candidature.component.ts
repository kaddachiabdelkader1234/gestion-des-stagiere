import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CandidatureService } from '../../../core/services/candidature.service';
import { AuthService } from '../../../core/services/auth.service';
import { ApiError } from '../../../core/http/api-error';
import {
  Stagiaire,
  STATUT_STAGIAIRE_BADGE,
  STATUT_STAGIAIRE_LABELS,
  TYPE_STAGE_LABELS,
  TYPE_STAGE_VALUES,
  TypeStage
} from '../../../core/models/stagiaire.model';

@Component({
  selector: 'app-ma-candidature',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ma-candidature.component.html'
})
export class MaCandidatureComponent implements OnInit {
  candidature: Stagiaire | null = null;
  chargement = true;
  envoiEnCours = false;
  erreur: string | null = null;
  succes: string | null = null;

  fichierCandidature: File | null = null;
  fichierCv: File | null = null;
  uploadEnCours = false;
  uploadType: 'candidature' | 'cv' | null = null;

  readonly typeStageLabels = TYPE_STAGE_LABELS;
  readonly typeStageValues = TYPE_STAGE_VALUES;
  readonly statutLabels = STATUT_STAGIAIRE_LABELS;
  readonly statutBadges = STATUT_STAGIAIRE_BADGE;

  // Form fields
  prenom = '';
  nom = '';
  email = '';
  typeStage: TypeStage = 'PFE';
  departement = '';
  dateDebut = '';
  dateFin = '';

  constructor(
    private candidatureService: CandidatureService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    const profile = this.authService.getUserProfile();
    if (profile) {
      this.prenom = profile.firstName || '';
      this.email = profile.email || '';
    }
    // Default dates: today → 3 months from now
    const today = new Date();
    this.dateDebut = today.toISOString().split('T')[0];
    const threeMonths = new Date(today);
    threeMonths.setMonth(threeMonths.getMonth() + 3);
    this.dateFin = threeMonths.toISOString().split('T')[0];

    this.chargerCandidature();
  }

  private chargerCandidature(): void {
    this.chargement = true;
    this.candidatureService.maCandidature().subscribe({
      next: candidature => {
        this.candidature = candidature;
        this.chargement = false;
      },
      error: (error: ApiError) => {
        if (error.status !== 404) {
          this.erreur = error.message;
        }
        this.candidature = null;
        this.chargement = false;
      }
    });
  }

  onFileSelected(event: Event, type: 'candidature' | 'cv'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (type === 'candidature') {
      this.fichierCandidature = file;
    } else {
      this.fichierCv = file;
    }
  }

  /** Check if the form is valid enough to submit. */
  get formulaireValide(): boolean {
    return !!(
      this.nom.trim() &&
      this.prenom.trim() &&
      this.email.trim() &&
      this.departement.trim() &&
      this.typeStage &&
      this.dateDebut &&
      this.dateFin &&
      this.fichierCandidature &&
      this.fichierCv
    );
  }

  soumettreCandidature(): void {
    if (!this.formulaireValide || this.envoiEnCours) return;

    this.envoiEnCours = true;
    this.erreur = null;

    const data = {
      nom: this.nom.trim(),
      prenom: this.prenom.trim(),
      email: this.email.trim().toLowerCase(),
      departement: this.departement.trim(),
      typeStage: this.typeStage,
      ecole: 'À définir',
      dateDebut: this.dateDebut,
      dateFin: this.dateFin
    };

    this.candidatureService.soumettre(data).subscribe({
      next: candidature => {
        this.candidature = candidature;
        this.envoiEnCours = false;
        // Upload both files
        this.uploadDocument(candidature.id, this.fichierCandidature!, 'candidature');
      },
      error: (error: ApiError) => {
        this.envoiEnCours = false;
        this.erreur = error.message;
      }
    });
  }

  uploadDocument(candidatureId: string, file: File, type: 'candidature' | 'cv'): void {
    this.uploadEnCours = true;
    this.uploadType = type;

    const call$ = type === 'candidature'
      ? this.candidatureService.televerserDocument(candidatureId, file)
      : this.candidatureService.televerserCv(candidatureId, file);

    call$.subscribe({
      next: candidature => {
        this.candidature = candidature;
        this.uploadEnCours = false;
        this.uploadType = null;
        this.succes = type === 'candidature'
          ? 'Formulaire de candidature envoyé.'
          : 'CV envoyé.';

        // Chain: after candidature doc, upload CV
        if (type === 'candidature' && this.fichierCv) {
          this.uploadDocument(candidatureId, this.fichierCv, 'cv');
        }

        if (type === 'candidature') this.fichierCandidature = null;
        if (type === 'cv') this.fichierCv = null;
      },
      error: (error: ApiError) => {
        this.uploadEnCours = false;
        this.uploadType = null;
        this.erreur = error.fieldErrors?.['fichier']?.[0] ?? error.message;
      }
    });
  }

  remplacerDocument(): void {
    if (!this.candidature || !this.fichierCandidature || this.uploadEnCours) return;
    this.uploadDocument(this.candidature.id, this.fichierCandidature, 'candidature');
  }

  remplacerCv(): void {
    if (!this.candidature || !this.fichierCv || this.uploadEnCours) return;
    this.uploadDocument(this.candidature.id, this.fichierCv, 'cv');
  }

  telechargerCv(): void {
    if (!this.candidature?.cvDisponible) return;
    this.candidatureService.telechargerCv(this.candidature.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = this.candidature?.cvNomFichier ?? 'cv.pdf';
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  telechargerDocument(): void {
    if (!this.candidature?.documentDisponible) return;
    this.candidatureService.telechargerDocument(this.candidature.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = this.candidature?.documentNomFichier ?? 'candidature.pdf';
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: ApiError) => (this.erreur = error.message)
    });
  }

  get decisionPrise(): boolean {
    return this.candidature?.statut === 'Acceptee' || this.candidature?.statut === 'Rejetee';
  }
}
