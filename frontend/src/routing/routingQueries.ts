import { useQuery } from '@tanstack/react-query';
import { getProduct, getRouting } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';

export function useProduct(productId: number) {
  return useQuery({ queryKey: queryKeys.product(productId), queryFn: () => getProduct(productId) });
}

export function useRouting(productId: number) {
  return useQuery({ queryKey: queryKeys.routing(productId), queryFn: () => getRouting(productId) });
}
