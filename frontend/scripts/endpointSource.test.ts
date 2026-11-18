// @vitest-environment node
// The generator runs in Node, not in the browser.
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { endpointsFile, readOpenApiDocument } from './apiFiles.ts';
import { endpointSourceOf, type OpenApiDocument, type OpenApiPathItem } from './endpointSource.ts';

const documentOf = (paths: Record<string, OpenApiPathItem>): OpenApiDocument => ({ paths });

describe('endpointSourceOf', () => {
  it("AC_165 writes one function per operation that sends the operation's method and url and returns its response type", () => {
    const source = endpointSourceOf(
      documentOf({
        '/api/v1/things': {
          get: { operationId: 'ListThings', parameters: [{ name: 'page', in: 'query' }] },
          post: { operationId: 'CreateThing', requestBody: { required: true } },
        },
        '/api/v1/things/{thingId}/parts/{id}': {
          put: {
            operationId: 'UpdatePart',
            parameters: [
              { name: 'id', in: 'path' },
              { name: 'thingId', in: 'path' },
              { name: 'dryRun', in: 'query' },
            ],
            requestBody: { required: true },
          },
          delete: {
            operationId: 'DeletePart',
            parameters: [
              { name: 'thingId', in: 'path' },
              { name: 'id', in: 'path' },
            ],
          },
        },
      }),
    );

    expect(source.split('\n').slice(3).join('\n')).toBe(`
export const listThings = (query: NonNullable<Api.ListThingsData['query']>) =>
  requestApi<Api.ListThingsResponse>('GET', { url: '/api/v1/things', query } satisfies Api.ListThingsData);

export const createThing = (body: Api.CreateThingData['body']) =>
  requestApi<Api.CreateThingResponse>('POST', { url: '/api/v1/things', body } satisfies Api.CreateThingData);

export const updatePart = (thingId: Api.UpdatePartData['path']['thingId'], id: Api.UpdatePartData['path']['id'], body: Api.UpdatePartData['body'], query: NonNullable<Api.UpdatePartData['query']>) =>
  requestApi<Api.UpdatePartResponse>('PUT', { url: '/api/v1/things/{thingId}/parts/{id}', path: { thingId, id }, body, query } satisfies Api.UpdatePartData);

export const deletePart = (thingId: Api.DeletePartData['path']['thingId'], id: Api.DeletePartData['path']['id']) =>
  requestApi<Api.DeletePartResponse>('DELETE', { url: '/api/v1/things/{thingId}/parts/{id}', path: { thingId, id } } satisfies Api.DeletePartData);
`);
  });

  it.each<[string, Record<string, OpenApiPathItem>, string]>([
    ['an operation without an operation id', { '/api/v1/things': { get: {} } }, 'GET /api/v1/things has no operationId'],
    ['a path value the operation does not declare', { '/api/v1/things/{id}': { get: { operationId: 'GetThing' } } }, 'GetThing does not declare the path value "id"'],
    [
      'a declared path value missing from the url',
      { '/api/v1/things': { get: { operationId: 'ListThings', parameters: [{ name: 'id', in: 'path' }] } } },
      'ListThings declares the path value "id"',
    ],
    [
      'a parameter outside the path and query',
      { '/api/v1/things': { get: { operationId: 'ListThings', parameters: [{ name: 'X-Trace', in: 'header' }] } } },
      'ListThings has a header parameter',
    ],
    ['an optional body', { '/api/v1/things': { post: { operationId: 'CreateThing', requestBody: {} } } }, 'CreateThing has an optional body'],
    ['a method the fetch wrapper does not send', { '/api/v1/things': { patch: { operationId: 'PatchThing' } } }, 'PatchThing uses PATCH'],
  ])('AC_165 stops on %s', (_case, paths, message) => {
    expect(() => endpointSourceOf(documentOf(paths))).toThrow(message);
  });

  it('AC_165 the committed endpoint functions match the OpenAPI document (run `npm run generate:api` when this fails)', () => {
    expect(readFileSync(endpointsFile, 'utf8')).toBe(endpointSourceOf(readOpenApiDocument()));
  });
});
