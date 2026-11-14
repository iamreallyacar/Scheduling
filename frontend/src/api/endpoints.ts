// One function per API operation, typed by the generated OpenAPI types.
import type * as Api from './generated';
import { requestApi } from './request';

export const signIn = (body: Api.SignInRequest) =>
  requestApi<Api.SignInResponse>('POST', { url: '/api/v1/auth/sign-in', body } satisfies Api.SignInData);

export const signOut = () => requestApi<Api.SignOutResponse>('POST', { url: '/api/v1/auth/sign-out' } satisfies Api.SignOutData);

export const getCurrentUser = () => requestApi<Api.GetCurrentUserResponse>('GET', { url: '/api/v1/me' } satisfies Api.GetCurrentUserData);

export const listResourceTypes = () =>
  requestApi<Api.ListResourceTypesResponse>('GET', { url: '/api/v1/resource-types' } satisfies Api.ListResourceTypesData);

export const createResourceType = (body: Api.ResourceTypeRequest) =>
  requestApi<Api.CreateResourceTypeResponse>('POST', { url: '/api/v1/resource-types', body } satisfies Api.CreateResourceTypeData);

export const updateResourceType = (id: number, body: Api.ResourceTypeRequest) =>
  requestApi<Api.UpdateResourceTypeResponse>('PUT', { url: '/api/v1/resource-types/{id}', path: { id }, body } satisfies Api.UpdateResourceTypeData);

export const deleteResourceType = (id: number) =>
  requestApi<Api.DeleteResourceTypeResponse>('DELETE', { url: '/api/v1/resource-types/{id}', path: { id } } satisfies Api.DeleteResourceTypeData);

export const listResources = (query: NonNullable<Api.ListResourcesData['query']>) =>
  requestApi<Api.ListResourcesResponse>('GET', { url: '/api/v1/resources', query } satisfies Api.ListResourcesData);

export const createResource = (body: Api.CreateResourceRequest) =>
  requestApi<Api.CreateResourceResponse>('POST', { url: '/api/v1/resources', body } satisfies Api.CreateResourceData);

export const updateResource = (id: number, body: Api.UpdateResourceRequest) =>
  requestApi<Api.UpdateResourceResponse>('PUT', { url: '/api/v1/resources/{id}', path: { id }, body } satisfies Api.UpdateResourceData);

export const deleteResource = (id: number) =>
  requestApi<Api.DeleteResourceResponse>('DELETE', { url: '/api/v1/resources/{id}', path: { id } } satisfies Api.DeleteResourceData);

export const listProducts = () => requestApi<Api.ListProductsResponse>('GET', { url: '/api/v1/products' } satisfies Api.ListProductsData);

export const getProduct = (id: number) =>
  requestApi<Api.GetProductResponse>('GET', { url: '/api/v1/products/{id}', path: { id } } satisfies Api.GetProductData);

export const createProduct = (body: Api.ProductRequest) =>
  requestApi<Api.CreateProductResponse>('POST', { url: '/api/v1/products', body } satisfies Api.CreateProductData);

export const updateProduct = (id: number, body: Api.ProductRequest) =>
  requestApi<Api.UpdateProductResponse>('PUT', { url: '/api/v1/products/{id}', path: { id }, body } satisfies Api.UpdateProductData);

export const deleteProduct = (id: number) =>
  requestApi<Api.DeleteProductResponse>('DELETE', { url: '/api/v1/products/{id}', path: { id } } satisfies Api.DeleteProductData);

export const getRouting = (productId: number) =>
  requestApi<Api.GetRoutingResponse>('GET', { url: '/api/v1/products/{productId}/routing', path: { productId } } satisfies Api.GetRoutingData);

export const replaceRouting = (productId: number, body: Api.RoutingRequest) =>
  requestApi<Api.ReplaceRoutingResponse>('PUT', { url: '/api/v1/products/{productId}/routing', path: { productId }, body } satisfies Api.ReplaceRoutingData);
