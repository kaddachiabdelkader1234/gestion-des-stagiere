/**
 * Mirrors Evaluation.Service's DTOs. Keep this file in sync with:
 *   Evaluation.Service/DTOs/EvaluationReadDto.cs
 *   Evaluation.Service/DTOs/EvaluationCreateDto.cs
 *   Evaluation.Service/DTOs/EvaluationUpdateDto.cs
 *   Evaluation.Service/DTOs/EvaluationQueryParameters.cs
 *   Evaluation.Service/Models/Evaluation.cs  (enums)
 *   Evaluation.Service/Validation/EvaluationValidators.cs  (the limits below)
 */

/** Evaluation type — the brief wants a competency grid; the deployed model has two fixed types. */
export type TypeEvaluation = 'MiParcours' | 'Finale';

export const TYPE_EVALUATION_VALUES: readonly TypeEvaluation[] = ['MiParcours', 'Finale'] as const;

/** French labels for display. */
export const TYPE_EVALUATION_LABELS: Record<TypeEvaluation, string> = {
  MiParcours: 'Mi-parcours',
  Finale: 'Finale'
};

/** Soft UI badge classes per type. */
export const TYPE_EVALUATION_BADGE: Record<TypeEvaluation, string> = {
  MiParcours: 'bg-blue-100 text-blue-800',
  Finale: 'bg-purple-100 text-purple-800'
};

/** Evaluation status — created as Soumise, validated by admin to Validee. */
export type StatutEvaluation = 'EnAttente' | 'Soumise' | 'Validee';

export const STATUT_EVALUATION_VALUES: readonly StatutEvaluation[] = [
  'EnAttente',
  'Soumise',
  'Validee'
] as const;

/** French labels for display. */
export const STATUT_EVALUATION_LABELS: Record<StatutEvaluation, string> = {
  EnAttente: 'En attente',
  Soumise: 'Soumise',
  Validee: 'Validée'
};

/** Soft UI badge classes per status. */
export const STATUT_EVALUATION_BADGE: Record<StatutEvaluation, string> = {
  EnAttente: 'bg-yellow-100 text-yellow-800',
  Soumise: 'bg-blue-100 text-blue-800',
  Validee: 'bg-green-100 text-green-800'
};

/**
 * An evaluation as returned by GET /api/v1/evaluations.
 *
 * The stagiaire's identity is denormalised at create time from the StagiaireAffectation projection,
 * so a list screen never has to join against Stagiaire.Service.
 */
export interface Evaluation {
  id: string;
  stagiaireId: string;
  /** auth-service user id of the owning learner — what scopes a LEARNER's reads. */
  utilisateurId?: number | null;
  /** auth-service user id of the assigned encadrant — what scopes a TRAINER's reads. */
  encadrantId: number;
  /** Denormalised from the projection, so a list renders without a lookup per row. */
  stagiaireNom: string;
  stagiairePrenom: string;
  typeEvaluation: TypeEvaluation;
  /** ISO date, "yyyy-MM-dd" — the API uses DateOnly, so no time component. */
  dateEvaluation: string;
  /** Score out of 20. */
  note: number;
  commentaire: string;
  statut: StatutEvaluation;
}

/** Body for POST /api/v1/evaluations — the encadrant form. */
export interface EvaluationCreateRequest {
  stagiaireId: string;
  typeEvaluation: TypeEvaluation;
  dateEvaluation: string;
  /** Score out of 20. */
  note: number;
  commentaire: string;
}

/** Body for PUT /api/v1/evaluations/{id}. */
export interface EvaluationUpdateRequest {
  typeEvaluation: TypeEvaluation;
  dateEvaluation: string;
  note: number;
  commentaire: string;
}

/** Query parameters for GET /api/v1/evaluations. */
export interface EvaluationQuery {
  page?: number;
  pageSize?: number;
  stagiaireId?: string;
  /** Effective for ADMIN only — a TRAINER is already scoped by the visibility filter. */
  encadrantId?: number;
  statut?: StatutEvaluation;
  typeEvaluation?: TypeEvaluation;
}

/**
 * Server-side limits, mirrored here so a form can reject bad input before a round-trip.
 * The server remains the authority — these only save the user a failed request.
 */
export const EVALUATION_LIMITS = {
  noteMin: 0,
  noteMax: 20,
  /** One decimal place — the column is numeric(3,1), so 15.25 would be silently rounded. */
  noteDecimals: 1,
  commentaireMin: 10,
  commentaireMax: 4000
} as const;
