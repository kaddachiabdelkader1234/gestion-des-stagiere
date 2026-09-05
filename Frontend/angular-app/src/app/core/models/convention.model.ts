/**
 * Mirrors Convention.Service's DTOs. Keep this file in sync with:
 *   Convention.Service/DTOs/ConventionReadDto.cs
 *   Convention.Service/DTOs/ConventionQueryParameters.cs
 *   Convention.Service/Models/StatutSignature.cs
 */

/**
 * Values as the API serializes them. Convention.Service registers a JsonStringEnumConverter, so the
 * C# member name crosses the wire — "EnAttente", not "EN_ATTENTE".
 *
 * The brief calls these BROUILLON → SIGNEE; the deployed enum is `EnAttente | Signee | Refusee`.
 * This union matches the deployed contract, and the labels below carry the brief's wording.
 */
export type StatutSignature = 'EnAttente' | 'Signee' | 'Refusee';

export const STATUT_SIGNATURE_VALUES: readonly StatutSignature[] = [
  'EnAttente',
  'Signee',
  'Refusee'
] as const;

/** French labels for display — the raw enum names are not user-facing text. */
export const STATUT_SIGNATURE_LABELS: Record<StatutSignature, string> = {
  EnAttente: 'Brouillon',
  Signee: 'Signée',
  Refusee: 'Refusée'
};

/** Soft UI badge classes per status, matching STATUT_STAGIAIRE_BADGE's palette. */
export const STATUT_SIGNATURE_BADGE: Record<StatutSignature, string> = {
  EnAttente: 'bg-yellow-100 text-yellow-800',
  Signee: 'bg-green-100 text-green-800',
  Refusee: 'bg-red-100 text-red-800'
};

/**
 * A convention as returned by GET /api/v1/conventions.
 *
 * The stagiaire's identity is denormalised onto the row when the candidature is accepted, so this
 * screen never has to join against Stagiaire.Service.
 *
 * There is no `cheminPdf`: the storage path is deliberately not exposed. Use `pdfDisponible` to
 * decide whether to offer a download, and fetch the bytes from GET /api/v1/conventions/{id}/pdf.
 */
export interface Convention {
  id: string;
  stagiaireId: string;
  /** auth-service user id of the owning learner; what the server scopes a LEARNER's reads against. */
  utilisateurId?: number | null;
  /** auth-service user id of the assigned encadrant; what it scopes a TRAINER's reads against. */
  encadrantId?: number | null;
  stagiaireNom: string;
  stagiairePrenom: string;
  stagiaireEmail: string;
  departement: string;
  /** ISO date, "yyyy-MM-dd" — the API uses DateOnly, so there is no time component. */
  dateDebut: string;
  dateFin: string;
  dateGeneration: string;
  statutSignature: StatutSignature;
  pdfDisponible: boolean;
}

/** Query parameters for GET /api/v1/conventions. */
export interface ConventionQuery {
  page?: number;
  pageSize?: number;
  statutSignature?: StatutSignature;
  /** The Guid of the stagiaire record, i.e. `Stagiaire.id` — not the auth-service user id. */
  stagiaireId?: string;
}
