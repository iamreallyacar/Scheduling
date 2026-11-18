// Writes the endpoint functions after `openapi-ts` has written the types (DEC-37).
import { writeFileSync } from 'node:fs';
import { endpointsFile, readOpenApiDocument } from './apiFiles.ts';
import { endpointSourceOf } from './endpointSource.ts';

writeFileSync(endpointsFile, endpointSourceOf(readOpenApiDocument()));
console.log(`Wrote ${endpointsFile}`);
