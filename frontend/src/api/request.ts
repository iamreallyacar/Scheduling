import type { AntiforgeryTokenResponse, GetAntiforgeryTokenData } from './generated';
import { ApiProblemError, PROBLEM_CODE_AUTH_ANTIFORGERY, readProblem, unreachableServerProblem } from './problem';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

// The shape of the generated `...Data` types: what one endpoint takes and where it lives.
interface EndpointData {
  url: string;
  path?: Record<string, string | number> | undefined;
  query?: Record<string, string | number | boolean | undefined> | undefined;
  body?: unknown;
}

// The token belongs to the session (see AntiforgeryProtection in the API), so it is fetched
// once and forgotten whenever the session changes.
let antiforgeryToken: Promise<AntiforgeryTokenResponse> | null = null;

export function forgetAntiforgeryToken(): void {
  antiforgeryToken = null;
}

/**
 * Calls one API endpoint. Callers pass `data` as the generated `...Data` type of the endpoint
 * (with `satisfies`) and name its generated response type, so url, path values, query and
 * body are checked against the OpenAPI document.
 * Any answer that is not a success is thrown as ApiProblemError.
 */
export async function requestApi<TResponse>(method: HttpMethod, data: EndpointData): Promise<TResponse> {
  const headers = new Headers({ Accept: 'application/json' });
  const init: RequestInit = { method, headers, credentials: 'same-origin' };
  if (data.body !== undefined) {
    headers.set('Content-Type', 'application/json');
    init.body = JSON.stringify(data.body);
  }
  if (method !== 'GET') {
    const { headerName, token } = await currentAntiforgeryToken();
    headers.set(headerName, token);
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(data), init);
  } catch (error) {
    // fetch rejects only when no answer arrived at all.
    throw new ApiProblemError(unreachableServerProblem(), { cause: error });
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    if (problem.code === PROBLEM_CODE_AUTH_ANTIFORGERY) {
      forgetAntiforgeryToken();
    }
    throw new ApiProblemError(problem);
  }

  // The body is trusted to match the OpenAPI document the types were generated from; the
  // backend test OpenApiDocumentTests keeps the two equal. 204 has no body.
  return (response.status === 204 ? undefined : await response.json()) as TResponse;
}

function currentAntiforgeryToken(): Promise<AntiforgeryTokenResponse> {
  antiforgeryToken ??= requestApi<AntiforgeryTokenResponse>('GET', { url: '/api/v1/auth/antiforgery-token' } satisfies GetAntiforgeryTokenData).catch(
    (error: unknown) => {
      forgetAntiforgeryToken();
      throw error;
    },
  );
  return antiforgeryToken;
}

function buildUrl(data: EndpointData): string {
  const path = data.url.replace(/\{(\w+)\}/g, (_placeholder, name: string) => {
    const value = data.path?.[name];
    if (value === undefined) {
      throw new Error(`No value for "${name}" in ${data.url}.`);
    }
    return encodeURIComponent(String(value));
  });

  const search = new URLSearchParams();
  for (const [name, value] of Object.entries(data.query ?? {})) {
    if (value !== undefined) {
      search.set(name, String(value));
    }
  }
  return search.size > 0 ? `${path}?${search.toString()}` : path;
}
