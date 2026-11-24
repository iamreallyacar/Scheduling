import type { Query } from '@tanstack/react-query';
import type { MachineFilter } from '../machines/machineQueries';
import type { OrderListQuery } from '../orders/orderListQuery';

// Every query key of the app (DEC-36). A key starts with its area, so all queries of an area
// share the first element.
export const queryKeys = {
  currentUser: ['current-user'],
  resourceTypes: ['resource-types'],
  resources: (filter: MachineFilter) => ['resources', filter],
  resource: (resourceId: number) => ['resources', resourceId],
  unavailabilities: (resourceId: number) => ['resources', resourceId, 'unavailabilities'],
  products: ['products'],
  product: (productId: number) => ['products', productId],
  routing: (productId: number) => ['products', productId, 'routing'],
  calendars: ['calendars'],
  holidays: ['holidays'],
  orderPage: (query: OrderListQuery) => ['orders', 'page', query],
  order: (orderId: number) => ['orders', orderId],
  unscheduledLots: (versionId: string) => ['versions', versionId, 'unscheduled-lots'],
  conflictedPlacements: (versionId: string) => ['versions', versionId, 'conflicts'],
  timeline: (versionId: string, range: { from: string; to: string }) => ['versions', versionId, 'timeline', range],
  earliestSlot: (versionId: string, lotId: number, search: { resourceId?: number; notBefore?: string }) => ['versions', versionId, 'lots', lotId, 'earliest-slot', search],
  dashboard: (versionId: string) => ['versions', versionId, 'dashboard'],
  asapProposal: (versionId: string, orderId: number, search: { notBefore?: string }) => ['versions', versionId, 'orders', orderId, 'asap-proposal', search],
} as const;

export function isCurrentUserQuery(query: Query): boolean {
  return query.queryKey[0] === queryKeys.currentUser[0];
}
