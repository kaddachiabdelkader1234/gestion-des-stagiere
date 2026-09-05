export interface AuditEntry {
  id: number;
  action: string;
  userId: number | null;
  userRole: string | null;
  userEmail: string | null;
  entityType: string;
  entityId: string | null;
  details: string | null;
  timestamp: string;
  traceId: string | null;
}

export interface AuditQueryParams {
  action?: string;
  entityType?: string;
  userId?: number;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface AuditPage {
  items: AuditEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Human-readable labels for audit action types. */
export const AUDIT_ACTION_LABELS: Record<string, string> = {
  'CANDIDATURE_SUBMITTED': 'Candidature soumise',
  'CANDIDATURE_ACCEPTED': 'Candidature acceptée',
  'CANDIDATURE_REJECTED': 'Candidature rejetée',
  'CONVENTION_GENERATED': 'Convention générée',
  'CONVENTION_SIGNED': 'Convention signée',
  'EVALUATION_CREATED': 'Évaluation créée',
  'EVALUATION_SUBMITTED': 'Évaluation soumise',
  'EVALUATION_VALIDATED': 'Évaluation validée',
  'JOURNAL_COMMENT': 'Commentaire journal',
  'ENCADRANT_CREATED': 'Encadrant créé',
};

/** Human-readable labels for entity types. */
export const AUDIT_ENTITY_LABELS: Record<string, string> = {
  'Stagiaire': 'Stagiaire',
  'Convention': 'Convention',
  'Evaluation': 'Évaluation',
  'JournalEntry': 'Journal de bord',
  'Encadrant': 'Encadrant',
};

/** CSS classes for action badges. */
export function auditActionBadgeClass(action: string): string {
  if (action.includes('ACCEPTED') || action.includes('VALIDATED') || action.includes('SIGNED')) {
    return 'bg-green-100 text-green-800';
  }
  if (action.includes('REJECTED')) {
    return 'bg-red-100 text-red-800';
  }
  if (action.includes('SUBMITTED') || action.includes('CREATED') || action.includes('GENERATED')) {
    return 'bg-blue-100 text-blue-800';
  }
  if (action.includes('COMMENT')) {
    return 'bg-purple-100 text-purple-800';
  }
  return 'bg-gray-100 text-gray-800';
}
