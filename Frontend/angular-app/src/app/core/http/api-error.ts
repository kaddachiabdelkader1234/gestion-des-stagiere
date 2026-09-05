import { HttpErrorResponse } from '@angular/common/http';

/**
 * Error shape the .NET services return, e.g.
 *   { "error": "Le nom est obligatoire.", "code": "VALIDATION_ERROR" }
 *
 * ASP.NET's built-in ProblemDetails / FluentValidation responses are also handled by
 * {@link toApiError} so the UI has one thing to render regardless of which layer failed.
 */
export interface ApiError {
  /** Message safe to show the user. Never a stack trace. */
  message: string;
  /** Machine-readable code when the service supplies one. */
  code?: string;
  /** HTTP status, or 0 when the request never reached the gateway. */
  status: number;
  /** Per-field validation messages, keyed by field name. */
  fieldErrors?: Record<string, string[]>;
}

/**
 * Normalizes anything HttpClient can throw into an {@link ApiError}.
 *
 * Services must let errors reach the caller — swallowing them into an empty value (`of([])`)
 * leaves the UI showing "no data" for what was actually a failed request, and in the case of
 * login it let a rejected sign-in navigate to the dashboard with no token.
 */
export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { message: 'Une erreur inattendue est survenue.', status: 0 };
  }

  // status 0 = network failure / CORS / gateway down. error.error is a ProgressEvent here.
  if (error.status === 0) {
    return {
      message: "Impossible de joindre le serveur. Vérifiez que la passerelle API est démarrée.",
      code: 'NETWORK_ERROR',
      status: 0
    };
  }

  const body = error.error;

  // A 401 from the gateway has an empty body — HttpStatusServerEntryPoint writes status only.
  if (error.status === 401) {
    return { message: 'Session expirée ou identifiants invalides.', code: 'UNAUTHORIZED', status: 401 };
  }

  if (error.status === 403) {
    return {
      message: "Vous n'avez pas les droits nécessaires pour cette action.",
      code: 'FORBIDDEN',
      status: 403
    };
  }

  return {
    message: extractMessage(body, error),
    code: typeof body?.code === 'string' ? body.code : undefined,
    status: error.status,
    fieldErrors: extractFieldErrors(body)
  };
}

function extractMessage(body: any, error: HttpErrorResponse): string {
  if (typeof body === 'string' && body.trim()) {
    return body;
  }

  // `error` is our own convention; `message`/`title`/`detail` cover auth-service and ProblemDetails.
  const candidate = body?.error ?? body?.message ?? body?.detail ?? body?.title;

  if (typeof candidate === 'string' && candidate.trim()) {
    return candidate;
  }

  return error.status >= 500
    ? 'Le service est momentanément indisponible. Réessayez plus tard.'
    : 'La requête a échoué.';
}

/**
 * Pulls out ASP.NET's ValidationProblemDetails `errors` map so a form can highlight
 * the offending fields instead of showing one generic message.
 */
function extractFieldErrors(body: any): Record<string, string[]> | undefined {
  const errors = body?.errors;

  if (!errors || typeof errors !== 'object') {
    return undefined;
  }

  const normalized: Record<string, string[]> = {};

  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages)) {
      normalized[field] = messages.filter((m): m is string => typeof m === 'string');
    } else if (typeof messages === 'string') {
      normalized[field] = [messages];
    }
  }

  return Object.keys(normalized).length ? normalized : undefined;
}
