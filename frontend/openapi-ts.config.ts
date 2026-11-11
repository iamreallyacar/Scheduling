import { defineConfig } from '@hey-api/openapi-ts';

// Generates the request and response types from the OpenAPI document that the backend tests keep up to
// date (ARCHITECTURE.md section 4). Run with `npm run generate:api`; never edit the output.
export default defineConfig({
  input: '../backend/openapi/tire-scheduler-v1.json',
  output: 'src/api/generated',
  plugins: ['@hey-api/typescript'],
});
