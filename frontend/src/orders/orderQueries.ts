import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getOrder, listOrders } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';
import type { OrderListQuery } from './orderListQuery';

// The previous page stays on screen while the next one loads.
export function useOrderPage(query: OrderListQuery) {
  return useQuery({ queryKey: queryKeys.orderPage(query), queryFn: () => listOrders(query), placeholderData: keepPreviousData });
}

export function useOrder(orderId: number) {
  return useQuery({ queryKey: queryKeys.order(orderId), queryFn: () => getOrder(orderId) });
}
