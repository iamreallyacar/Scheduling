// The parts of an OpenAPI document that the endpoint functions are written from.
export interface OpenApiDocument {
  paths: Record<string, OpenApiPathItem>;
}

export type OpenApiPathItem = Record<string, OpenApiOperation>;

export interface OpenApiOperation {
  operationId?: string;
  parameters?: OpenApiParameter[];
  requestBody?: { required?: boolean };
}

export interface OpenApiParameter {
  name: string;
  in: string;
}

// The methods `requestApi` in src/api/request.ts sends.
const SUPPORTED_METHODS = ['GET', 'POST', 'PUT', 'DELETE'];

const PATH_VALUE_PATTERN = /\{(\w+)\}/g;
const IDENTIFIER_PATTERN = /^[A-Za-z_$][\w$]*$/;

const HEADER = `// This file is written by \`npm run generate:api\` (scripts/generate-endpoints.ts) from the OpenAPI document; never edit it by hand.
import type * as Api from './types.gen';
import { requestApi } from '../request';
`;

/**
 * The source of `src/api/generated/endpoints.gen.ts`: one function per operation, in document
 * order, taking the path values in URL order, then the body, then the query (DEC-37).
 * Throws on an operation it cannot describe, so the generation stops instead of writing a wrong call.
 */
export function endpointSourceOf(document: OpenApiDocument): string {
  const functions = Object.entries(document.paths).flatMap(([url, pathItem]) =>
    Object.entries(pathItem).map(([method, operation]) => endpointFunctionOf(method.toUpperCase(), url, operation)),
  );
  return [HEADER, ...functions].join('\n');
}

function endpointFunctionOf(method: string, url: string, operation: OpenApiOperation): string {
  const operationId = operation.operationId;
  if (operationId === undefined || !IDENTIFIER_PATTERN.test(operationId)) {
    throw new Error(`${method} ${url} has no operationId that can name a function.`);
  }
  if (!SUPPORTED_METHODS.includes(method)) {
    throw new Error(`${operationId} uses ${method}, which requestApi in src/api/request.ts does not send.`);
  }

  const typeName = operationId.charAt(0).toUpperCase() + operationId.slice(1);
  const functionName = operationId.charAt(0).toLowerCase() + operationId.slice(1);
  const dataType = `Api.${typeName}Data`;

  const pathValues = pathValuesOf(operationId, url, operation.parameters ?? []);
  const hasQuery = (operation.parameters ?? []).some((parameter) => parameter.in === 'query');
  const hasBody = operation.requestBody !== undefined;
  if (hasBody && operation.requestBody?.required !== true) {
    throw new Error(`${operationId} has an optional body; extend scripts/endpointSource.ts to support it.`);
  }

  const parameters = [
    ...pathValues.map((name) => `${name}: ${dataType}['path']['${name}']`),
    ...(hasBody ? [`body: ${dataType}['body']`] : []),
    ...(hasQuery ? [`query: NonNullable<${dataType}['query']>`] : []),
  ];
  const data = [
    `url: '${url}'`,
    ...(pathValues.length > 0 ? [`path: { ${pathValues.join(', ')} }`] : []),
    ...(hasBody ? ['body'] : []),
    ...(hasQuery ? ['query'] : []),
  ];

  return `export const ${functionName} = (${parameters.join(', ')}) =>
  requestApi<Api.${typeName}Response>('${method}', { ${data.join(', ')} } satisfies ${dataType});
`;
}

// The path values in the order they appear in the URL, checked against the declared parameters.
function pathValuesOf(operationId: string, url: string, parameters: OpenApiParameter[]): string[] {
  for (const parameter of parameters) {
    if (parameter.in !== 'path' && parameter.in !== 'query') {
      throw new Error(`${operationId} has a ${parameter.in} parameter; only path and query parameters are supported.`);
    }
  }

  const declared = parameters.filter((parameter) => parameter.in === 'path').map((parameter) => parameter.name);
  const inUrl = Array.from(url.matchAll(PATH_VALUE_PATTERN), (match) => match[1] ?? '');
  for (const name of inUrl) {
    if (!declared.includes(name)) {
      throw new Error(`${operationId} does not declare the path value "${name}" of ${url}.`);
    }
  }
  for (const name of declared) {
    if (!inUrl.includes(name)) {
      throw new Error(`${operationId} declares the path value "${name}", which ${url} does not contain.`);
    }
  }
  return inUrl;
}
