import { defineConfig } from '@hey-api/openapi-ts';
import { generatedApiFolder, openApiDocumentFile } from './scripts/apiFiles.ts';

// Generates the request and response types from the OpenAPI document that the backend tests keep up to
// date (API.md section "Conventions"). Run with `npm run generate:api`, which then writes the endpoint
// functions with scripts/generate-endpoints.ts (DEC-37); never edit the output.
export default defineConfig({
  input: openApiDocumentFile,
  output: generatedApiFolder,
  plugins: ['@hey-api/typescript'],
});
