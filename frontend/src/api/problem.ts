// The error format of the API (ARCHITECTURE.md section 7). The UI shows `detail` to the user
// and uses `code` for behavior.
export interface ApiProblem {
  status: number;
  code: string;
  detail: string;
  /** Field name to messages. Empty when the problem is not about single fields. */
  errors: Readonly<Record<string, readonly string[]>>;
  correlationId: string | null;
}

export const PROBLEM_CODE_AUTH_REQUIRED = 'auth.required';
export const PROBLEM_CODE_AUTH_ANTIFORGERY = 'auth.antiforgery';

export class ApiProblemError extends Error {
  readonly problem: ApiProblem;

  constructor(problem: ApiProblem, options?: ErrorOptions) {
    super(problem.detail, options);
    this.name = 'ApiProblemError';
    this.problem = problem;
  }
}

export function hasProblemCode(error: unknown, code: string): boolean {
  return error instanceof ApiProblemError && error.problem.code === code;
}

/** The problem to show for a failed query or mutation; null when nothing failed. */
export function problemOf(error: unknown): ApiProblem | null {
  if (error === null || error === undefined) {
    return null;
  }
  if (error instanceof ApiProblemError) {
    return error.problem;
  }
  // Not an answer from the API, so a fault in this app. Rethrowing during render would blank
  // the page; the message keeps the page usable and the console keeps the cause.
  console.error(error);
  return { status: 0, code: 'client.error', detail: 'Something went wrong in the app. Reload the page and try again.', errors: {}, correlationId: null };
}

export function unreachableServerProblem(): ApiProblem {
  return { status: 0, code: 'network.unreachable', detail: 'The server could not be reached. Check your connection and try again.', errors: {}, correlationId: null };
}

export async function readProblem(response: Response): Promise<ApiProblem> {
  const fallback: ApiProblem = {
    status: response.status,
    code: `http.${String(response.status)}`,
    detail: 'The server answered in an unexpected way.',
    errors: {},
    correlationId: null,
  };
  if (response.headers.get('Content-Type')?.includes('application/problem+json') !== true) {
    return fallback;
  }

  const body: unknown = await response.json();
  if (!isRecord(body)) {
    return fallback;
  }
  return {
    status: response.status,
    code: typeof body['code'] === 'string' ? body['code'] : fallback.code,
    detail: typeof body['detail'] === 'string' ? body['detail'] : fallback.detail,
    errors: readFieldErrors(body['errors']),
    correlationId: typeof body['correlationId'] === 'string' ? body['correlationId'] : null,
  };
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }
  const fieldErrors: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      fieldErrors[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }
  return fieldErrors;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
