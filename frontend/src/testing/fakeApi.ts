import { vi } from 'vitest';
import type { CurrentUserResponse } from '../api/generated';

export interface FakeApiRequest {
  method: string;
  path: string;
  query: URLSearchParams;
  body: unknown;
  headers: Headers;
}

export interface FakeApiAnswer {
  status: number;
  body?: unknown;
}

type FakeApiRoute = (request: FakeApiRequest) => FakeApiAnswer;

export const ANTIFORGERY_HEADER = 'X-TEST-ANTIFORGERY';
export const ANTIFORGERY_TOKEN = 'test-antiforgery-token';

export const adminUser: CurrentUserResponse = { name: 'Ada Admin', email: 'ada@plant.test', role: 'Admin', canPlan: true, canAdminister: true };
export const plannerUser: CurrentUserResponse = { name: 'Pat Planner', email: 'pat@plant.test', role: 'Planner', canPlan: true, canAdminister: false };
export const viewerUser: CurrentUserResponse = { name: 'Vic Viewer', email: 'vic@plant.test', role: 'Viewer', canPlan: false, canAdminister: false };

export function problem(status: number, code: string, detail: string, errors?: Record<string, string[]>): FakeApiAnswer {
  return { status, body: { status, code, detail, ...(errors ? { errors } : {}) } };
}

export const notSignedIn = (): FakeApiAnswer => problem(401, 'auth.required', 'Sign in to continue.');

/**
 * Replaces `fetch` with a table of routes keyed by "METHOD /path". A request with no route
 * fails the test. Returns the requests made, in order.
 */
export function stubApi(routes: Record<string, FakeApiRoute>): FakeApiRequest[] {
  const requests: FakeApiRequest[] = [];
  const allRoutes: Record<string, FakeApiRoute> = {
    'GET /api/v1/auth/antiforgery-token': () => ({ status: 200, body: { headerName: ANTIFORGERY_HEADER, token: ANTIFORGERY_TOKEN } }),
    ...routes,
  };

  vi.stubGlobal('fetch', (input: string, init?: RequestInit): Promise<Response> => {
    const url = new URL(input, 'http://localhost');
    const request: FakeApiRequest = {
      method: init?.method ?? 'GET',
      path: url.pathname,
      query: url.searchParams,
      body: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined,
      headers: new Headers(init?.headers),
    };
    requests.push(request);

    const route = allRoutes[`${request.method} ${request.path}`];
    if (route === undefined) {
      return Promise.reject(new Error(`The test has no route for ${request.method} ${request.path}.`));
    }
    const answer = route(request);
    const contentType = answer.status >= 400 ? 'application/problem+json' : 'application/json';
    return Promise.resolve(
      new Response(answer.body === undefined ? null : JSON.stringify(answer.body), { status: answer.status, headers: { 'Content-Type': contentType } }),
    );
  });
  return requests;
}
