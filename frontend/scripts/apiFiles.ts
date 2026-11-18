import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { OpenApiDocument } from './endpointSource.ts';

// The OpenAPI document that the backend test OpenApiDocumentTests keeps up to date (DEC-27).
export const openApiDocumentFile = fileURLToPath(new URL('../../backend/openapi/tire-scheduler-v1.json', import.meta.url));

export const generatedApiFolder = fileURLToPath(new URL('../src/api/generated', import.meta.url));

export const endpointsFile = fileURLToPath(new URL('../src/api/generated/endpoints.gen.ts', import.meta.url));

export function readOpenApiDocument(): OpenApiDocument {
  // The backend writes the document from the API's own description, so its shape is trusted;
  // endpointSourceOf still throws on any operation it cannot describe.
  return JSON.parse(readFileSync(openApiDocumentFile, 'utf8')) as OpenApiDocument;
}
