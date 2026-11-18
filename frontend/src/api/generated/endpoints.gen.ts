// This file is written by `npm run generate:api` (scripts/generate-endpoints.ts) from the OpenAPI document; never edit it by hand.
import type * as Api from './types.gen';
import { requestApi } from '../request';

export const getAntiforgeryToken = () =>
  requestApi<Api.GetAntiforgeryTokenResponse>('GET', { url: '/api/v1/auth/antiforgery-token' } satisfies Api.GetAntiforgeryTokenData);

export const signIn = (body: Api.SignInData['body']) =>
  requestApi<Api.SignInResponse>('POST', { url: '/api/v1/auth/sign-in', body } satisfies Api.SignInData);

export const signOut = () =>
  requestApi<Api.SignOutResponse>('POST', { url: '/api/v1/auth/sign-out' } satisfies Api.SignOutData);

export const getCurrentUser = () =>
  requestApi<Api.GetCurrentUserResponse>('GET', { url: '/api/v1/me' } satisfies Api.GetCurrentUserData);

export const listResourceTypes = () =>
  requestApi<Api.ListResourceTypesResponse>('GET', { url: '/api/v1/resource-types' } satisfies Api.ListResourceTypesData);

export const createResourceType = (body: Api.CreateResourceTypeData['body']) =>
  requestApi<Api.CreateResourceTypeResponse>('POST', { url: '/api/v1/resource-types', body } satisfies Api.CreateResourceTypeData);

export const updateResourceType = (id: Api.UpdateResourceTypeData['path']['id'], body: Api.UpdateResourceTypeData['body']) =>
  requestApi<Api.UpdateResourceTypeResponse>('PUT', { url: '/api/v1/resource-types/{id}', path: { id }, body } satisfies Api.UpdateResourceTypeData);

export const deleteResourceType = (id: Api.DeleteResourceTypeData['path']['id']) =>
  requestApi<Api.DeleteResourceTypeResponse>('DELETE', { url: '/api/v1/resource-types/{id}', path: { id } } satisfies Api.DeleteResourceTypeData);

export const createResource = (body: Api.CreateResourceData['body']) =>
  requestApi<Api.CreateResourceResponse>('POST', { url: '/api/v1/resources', body } satisfies Api.CreateResourceData);

export const listResources = (query: NonNullable<Api.ListResourcesData['query']>) =>
  requestApi<Api.ListResourcesResponse>('GET', { url: '/api/v1/resources', query } satisfies Api.ListResourcesData);

export const updateResource = (id: Api.UpdateResourceData['path']['id'], body: Api.UpdateResourceData['body']) =>
  requestApi<Api.UpdateResourceResponse>('PUT', { url: '/api/v1/resources/{id}', path: { id }, body } satisfies Api.UpdateResourceData);

export const deleteResource = (id: Api.DeleteResourceData['path']['id']) =>
  requestApi<Api.DeleteResourceResponse>('DELETE', { url: '/api/v1/resources/{id}', path: { id } } satisfies Api.DeleteResourceData);

export const getResource = (id: Api.GetResourceData['path']['id']) =>
  requestApi<Api.GetResourceResponse>('GET', { url: '/api/v1/resources/{id}', path: { id } } satisfies Api.GetResourceData);

export const listCalendars = () =>
  requestApi<Api.ListCalendarsResponse>('GET', { url: '/api/v1/calendars' } satisfies Api.ListCalendarsData);

export const replaceCalendarShifts = (id: Api.ReplaceCalendarShiftsData['path']['id'], body: Api.ReplaceCalendarShiftsData['body']) =>
  requestApi<Api.ReplaceCalendarShiftsResponse>('PUT', { url: '/api/v1/calendars/{id}/shifts', path: { id }, body } satisfies Api.ReplaceCalendarShiftsData);

export const listHolidays = () =>
  requestApi<Api.ListHolidaysResponse>('GET', { url: '/api/v1/holidays' } satisfies Api.ListHolidaysData);

export const createHoliday = (body: Api.CreateHolidayData['body']) =>
  requestApi<Api.CreateHolidayResponse>('POST', { url: '/api/v1/holidays', body } satisfies Api.CreateHolidayData);

export const deleteHoliday = (id: Api.DeleteHolidayData['path']['id']) =>
  requestApi<Api.DeleteHolidayResponse>('DELETE', { url: '/api/v1/holidays/{id}', path: { id } } satisfies Api.DeleteHolidayData);

export const listProducts = () =>
  requestApi<Api.ListProductsResponse>('GET', { url: '/api/v1/products' } satisfies Api.ListProductsData);

export const createProduct = (body: Api.CreateProductData['body']) =>
  requestApi<Api.CreateProductResponse>('POST', { url: '/api/v1/products', body } satisfies Api.CreateProductData);

export const getProduct = (id: Api.GetProductData['path']['id']) =>
  requestApi<Api.GetProductResponse>('GET', { url: '/api/v1/products/{id}', path: { id } } satisfies Api.GetProductData);

export const updateProduct = (id: Api.UpdateProductData['path']['id'], body: Api.UpdateProductData['body']) =>
  requestApi<Api.UpdateProductResponse>('PUT', { url: '/api/v1/products/{id}', path: { id }, body } satisfies Api.UpdateProductData);

export const deleteProduct = (id: Api.DeleteProductData['path']['id']) =>
  requestApi<Api.DeleteProductResponse>('DELETE', { url: '/api/v1/products/{id}', path: { id } } satisfies Api.DeleteProductData);

export const getRouting = (productId: Api.GetRoutingData['path']['productId']) =>
  requestApi<Api.GetRoutingResponse>('GET', { url: '/api/v1/products/{productId}/routing', path: { productId } } satisfies Api.GetRoutingData);

export const replaceRouting = (productId: Api.ReplaceRoutingData['path']['productId'], body: Api.ReplaceRoutingData['body']) =>
  requestApi<Api.ReplaceRoutingResponse>('PUT', { url: '/api/v1/products/{productId}/routing', path: { productId }, body } satisfies Api.ReplaceRoutingData);

export const createOrder = (body: Api.CreateOrderData['body']) =>
  requestApi<Api.CreateOrderResponse>('POST', { url: '/api/v1/orders', body } satisfies Api.CreateOrderData);

export const listOrders = (query: NonNullable<Api.ListOrdersData['query']>) =>
  requestApi<Api.ListOrdersResponse>('GET', { url: '/api/v1/orders', query } satisfies Api.ListOrdersData);

export const updateOrder = (id: Api.UpdateOrderData['path']['id'], body: Api.UpdateOrderData['body']) =>
  requestApi<Api.UpdateOrderResponse>('PUT', { url: '/api/v1/orders/{id}', path: { id }, body } satisfies Api.UpdateOrderData);

export const deleteOrder = (id: Api.DeleteOrderData['path']['id']) =>
  requestApi<Api.DeleteOrderResponse>('DELETE', { url: '/api/v1/orders/{id}', path: { id } } satisfies Api.DeleteOrderData);

export const getOrder = (id: Api.GetOrderData['path']['id']) =>
  requestApi<Api.GetOrderResponse>('GET', { url: '/api/v1/orders/{id}', path: { id } } satisfies Api.GetOrderData);

export const cancelOrder = (id: Api.CancelOrderData['path']['id']) =>
  requestApi<Api.CancelOrderResponse>('POST', { url: '/api/v1/orders/{id}/cancel', path: { id } } satisfies Api.CancelOrderData);

export const listUnavailabilities = (resourceId: Api.ListUnavailabilitiesData['path']['resourceId']) =>
  requestApi<Api.ListUnavailabilitiesResponse>('GET', { url: '/api/v1/resources/{resourceId}/unavailabilities', path: { resourceId } } satisfies Api.ListUnavailabilitiesData);

export const createUnavailability = (resourceId: Api.CreateUnavailabilityData['path']['resourceId'], body: Api.CreateUnavailabilityData['body']) =>
  requestApi<Api.CreateUnavailabilityResponse>('POST', { url: '/api/v1/resources/{resourceId}/unavailabilities', path: { resourceId }, body } satisfies Api.CreateUnavailabilityData);

export const updateUnavailability = (resourceId: Api.UpdateUnavailabilityData['path']['resourceId'], id: Api.UpdateUnavailabilityData['path']['id'], body: Api.UpdateUnavailabilityData['body']) =>
  requestApi<Api.UpdateUnavailabilityResponse>('PUT', { url: '/api/v1/resources/{resourceId}/unavailabilities/{id}', path: { resourceId, id }, body } satisfies Api.UpdateUnavailabilityData);

export const deleteUnavailability = (resourceId: Api.DeleteUnavailabilityData['path']['resourceId'], id: Api.DeleteUnavailabilityData['path']['id']) =>
  requestApi<Api.DeleteUnavailabilityResponse>('DELETE', { url: '/api/v1/resources/{resourceId}/unavailabilities/{id}', path: { resourceId, id } } satisfies Api.DeleteUnavailabilityData);

export const placeLot = (versionId: Api.PlaceLotData['path']['versionId'], lotId: Api.PlaceLotData['path']['lotId'], body: Api.PlaceLotData['body']) =>
  requestApi<Api.PlaceLotResponse>('PUT', { url: '/api/v1/versions/{versionId}/lots/{lotId}/placement', path: { versionId, lotId }, body } satisfies Api.PlaceLotData);

export const removeLotPlacement = (versionId: Api.RemoveLotPlacementData['path']['versionId'], lotId: Api.RemoveLotPlacementData['path']['lotId']) =>
  requestApi<Api.RemoveLotPlacementResponse>('DELETE', { url: '/api/v1/versions/{versionId}/lots/{lotId}/placement', path: { versionId, lotId } } satisfies Api.RemoveLotPlacementData);

export const listUnscheduledLots = (versionId: Api.ListUnscheduledLotsData['path']['versionId']) =>
  requestApi<Api.ListUnscheduledLotsResponse>('GET', { url: '/api/v1/versions/{versionId}/unscheduled-lots', path: { versionId } } satisfies Api.ListUnscheduledLotsData);

export const listConflictedPlacements = (versionId: Api.ListConflictedPlacementsData['path']['versionId']) =>
  requestApi<Api.ListConflictedPlacementsResponse>('GET', { url: '/api/v1/versions/{versionId}/conflicts', path: { versionId } } satisfies Api.ListConflictedPlacementsData);
