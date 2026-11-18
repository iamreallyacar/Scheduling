import { useQuery } from '@tanstack/react-query';
import { listResources, listResourceTypes } from '../api/generated/endpoints.gen';
import type { ListResourcesData } from '../api/generated';
import { queryKeys } from '../api/queryKeys';

export interface MachineFilter {
  resourceTypeId: number | null;
  isActive: boolean | null;
}

export function useResourceTypes() {
  return useQuery({ queryKey: queryKeys.resourceTypes, queryFn: listResourceTypes });
}

// The server filters and sorts (AC-30); the filter is only passed on.
export function useMachines(filter: MachineFilter) {
  return useQuery({
    queryKey: queryKeys.resources(filter),
    queryFn: () => {
      const query: NonNullable<ListResourcesData['query']> = {};
      if (filter.resourceTypeId !== null) {
        query.resourceTypeId = filter.resourceTypeId;
      }
      if (filter.isActive !== null) {
        query.isActive = filter.isActive;
      }
      return listResources(query);
    },
  });
}
