import { useQuery } from '@tanstack/react-query';
import { listProducts } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';

export function useProducts() {
  return useQuery({ queryKey: queryKeys.products, queryFn: listProducts });
}
