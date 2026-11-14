import { useQuery } from '@tanstack/react-query';
import { getProduct, getRouting } from '../api/endpoints';
import { productsQueryKey } from '../products/productQueries';

export const routingQueryKey = (productId: number) => [...productsQueryKey, productId, 'routing'] as const;

export function useProduct(productId: number) {
  return useQuery({ queryKey: [...productsQueryKey, productId], queryFn: () => getProduct(productId) });
}

export function useRouting(productId: number) {
  return useQuery({ queryKey: routingQueryKey(productId), queryFn: () => getRouting(productId) });
}
