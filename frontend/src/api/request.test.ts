import { ANTIFORGERY_HEADER, ANTIFORGERY_TOKEN, problem, stubApi } from '../testing/fakeApi';
import { createProduct, deleteProduct, listResources, updateResource } from './endpoints';
import { ApiProblemError } from './problem';
import { forgetAntiforgeryToken } from './request';

describe('requestApi', () => {
  beforeEach(forgetAntiforgeryToken);

  it('fills path values and query parameters into the url and leaves out unset filters', async () => {
    const requests = stubApi({
      'GET /api/v1/resources': () => ({ status: 200, body: [] }),
      'PUT /api/v1/resources/7': () => ({ status: 200, body: {} }),
    });

    await listResources({ resourceTypeId: 3 });
    await updateResource(7, { name: 'Press 1', resourceTypeId: 3, isActive: false });

    expect(requests[0]?.query.toString()).toBe('resourceTypeId=3');
    expect(requests.at(-1)?.body).toEqual({ name: 'Press 1', resourceTypeId: 3, isActive: false });
  });

  it('AC_7 sends the antiforgery token with state-changing requests and fetches it once', async () => {
    const requests = stubApi({
      'POST /api/v1/products': () => ({ status: 201, body: { id: 1, code: 'A', name: 'A', spec: null } }),
      'DELETE /api/v1/products/1': () => ({ status: 204 }),
    });

    await createProduct({ code: 'A', name: 'A' });
    await deleteProduct(1);

    expect(requests.map((request) => `${request.method} ${request.path}`)).toEqual([
      'GET /api/v1/auth/antiforgery-token',
      'POST /api/v1/products',
      'DELETE /api/v1/products/1',
    ]);
    expect(requests[1]?.headers.get(ANTIFORGERY_HEADER)).toBe(ANTIFORGERY_TOKEN);
    expect(requests[2]?.headers.get(ANTIFORGERY_HEADER)).toBe(ANTIFORGERY_TOKEN);
  });

  it('throws the problem details of a rejected request', async () => {
    stubApi({
      'POST /api/v1/products': () => problem(409, 'conflict.duplicate', 'Another item already uses this code.', { code: ['Another item already uses this code.'] }),
    });

    const rejection = await createProduct({ code: 'A', name: 'A' }).catch((error: unknown) => error);

    expect(rejection).toBeInstanceOf(ApiProblemError);
    expect((rejection as ApiProblemError).problem).toEqual({
      status: 409,
      code: 'conflict.duplicate',
      detail: 'Another item already uses this code.',
      errors: { code: ['Another item already uses this code.'] },
      correlationId: null,
    });
  });

  it('reports an unreachable server as a problem', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Failed to fetch')));

    const rejection = await listResources({}).catch((error: unknown) => error);

    expect((rejection as ApiProblemError).problem.code).toBe('network.unreachable');
  });
});
