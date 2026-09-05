/**
 * Mirrors Stagiaire.Service's DTOs. Keep this file in sync with:
 *   Stagiaire.Service/DTOs/StagiaireReadDto.cs
 *   Stagiaire.Service/DTOs/CandidatureCreateDto.cs
 *   Stagiaire.Service/DTOs/CandidatureDecisionDtos.cs
 *   Stagiaire.Service/Models/StatutStagiaire.cs, TypeStage.cs
 */

/**
 * Values as the API serializes them. Program.cs registers a JsonStringEnumConverter, so the C#
 * member name crosses the wire — "EnAttente", not "EN_ATTENTE".
 *
 * The brief describes candidature and stage as two tracks
 * (EN_ATTENTE → ACCEPTEE → REJETEE, then EN_COURS → TERMINE); the backend models them as one
 * linear enum because a record only ever moves forward through them.
 */
export type StatutStagiaire = 'EnAttente' | 'Acceptee' | 'Rejetee' | 'EnCours' | 'Termine';

export const STATUT_STAGIAIRE_VALUES: readonly StatutStagiaire[] = [
  'EnAttente',
  'Acceptee',
  'Rejetee',
  'EnCours',
  'Termine'
] as const;

/** French labels for display — the raw enum names are not user-facing text. */
export const STATUT_STAGIAIRE_LABELS: Record<StatutStagiaire, string> = {
  EnAttente: 'En attente',
  Acceptee: 'Acceptée',
  Rejetee: 'Rejetée',
  EnCours: 'En cours',
  Termine: 'Terminé'
};

/** Soft UI badge classes per status, so tables and cards colour consistently. */
export const STATUT_STAGIAIRE_BADGE: Record<StatutStagiaire, string> = {
  EnAttente: 'bg-yellow-100 text-yellow-800',
  Acceptee: 'bg-green-100 text-green-800',
  Rejetee: 'bg-red-100 text-red-800',
  EnCours: 'bg-blue-100 text-blue-800',
  Termine: 'bg-gray-100 text-gray-800'
};

/** Internship type — `PFE` keeps its acronym casing in the C# enum, so it does here too. */
export type TypeStage = 'PFE' | 'StageEte' | 'StageOuvrier';

export const TYPE_STAGE_VALUES: readonly TypeStage[] = ['PFE', 'StageEte', 'StageOuvrier'] as const;

export const TYPE_STAGE_LABELS: Record<TypeStage, string> = {
  PFE: 'PFE (projet de fin d\'études)',
  StageEte: 'Stage d\'été',
  StageOuvrier: 'Stage ouvrier'
};

/**
 * A stagiaire/candidature as returned by GET /api/v1/stagiaires and the candidature endpoints.
 *
 * `id` is a Guid string; `utilisateurId` and `encadrantId` are auth-service numeric user ids.
 */
export interface Stagiaire {
  id: string;
  utilisateurId?: number | null;
  nom: string;
  prenom: string;
  email: string;
  departement: string;
  typeStage: TypeStage;
  ecole: string;
  /** ISO date, "yyyy-MM-dd" — the API uses DateOnly, so there is no time component. */
  dateDebut: string;
  dateFin: string;
  motivation?: string | null;
  /** True when a CV was uploaded; fetch it from GET /api/v1/candidatures/{id}/cv. */
  cvDisponible: boolean;
  cvNomFichier?: string | null;
  /** True when the candidature document (university scan) was uploaded. */
  documentDisponible: boolean;
  documentNomFichier?: string | null;
  statut: StatutStagiaire;
  encadrantId?: number | null;
  encadrantNom?: string | null;
  motifRejet?: string | null;
  /** ISO timestamp (UTC). */
  dateSoumission: string;
  dateDecision?: string | null;
}

/**
 * Body for POST /api/v1/candidatures.
 *
 * No `statut`, `encadrantId` or `utilisateurId`: a new candidature is always EnAttente, the
 * encadrant is assigned by an admin, and the owner comes from the JWT.
 */
export interface CandidatureCreateRequest {
  nom: string;
  prenom: string;
  email: string;
  departement: string;
  typeStage: TypeStage;
  ecole: string;
  dateDebut: string;
  dateFin: string;
  motivation?: string;
}

/** Body for POST /api/v1/candidatures/{id}/accepter. */
export interface CandidatureAccepterRequest {
  /** Omit to keep the département the candidate requested. */
  departement?: string;
  encadrantId: number;
  encadrantNom: string;
  /** Supply both or neither. */
  dateDebut?: string;
  dateFin?: string;
}

/** Body for POST /api/v1/candidatures/{id}/rejeter. Reason is mandatory (min 10 chars). */
export interface CandidatureRejeterRequest {
  motifRejet: string;
}

/** Body for POST /api/v1/stagiaires — admin-only direct creation. */
export interface StagiaireCreateRequest {
  nom: string;
  prenom: string;
  email: string;
  departement: string;
  typeStage: TypeStage;
  ecole: string;
  dateDebut: string;
  dateFin: string;
  motivation?: string;
  statut?: StatutStagiaire;
  utilisateurId?: number | null;
}

/** Body for PUT /api/v1/stagiaires/{id} — a full replacement. */
export interface StagiaireUpdateRequest {
  nom: string;
  prenom: string;
  email: string;
  departement: string;
  typeStage: TypeStage;
  ecole: string;
  dateDebut: string;
  dateFin: string;
  motivation?: string;
  statut: StatutStagiaire;
  encadrantId?: number | null;
  encadrantNom?: string | null;
  motifRejet?: string | null;
}

/** Query parameters for GET /api/v1/stagiaires. */
export interface StagiaireQuery {
  page?: number;
  pageSize?: number;
  statut?: StatutStagiaire;
  departement?: string;
  typeStage?: TypeStage;
  /** Effective for ADMIN only — the server pins a TRAINER to their own id. */
  encadrantId?: number;
  recherche?: string;
}
