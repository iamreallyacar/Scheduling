import { useQuery, type QueryClient } from '@tanstack/react-query';
import { listResources, listResourceTypes } from '../api/endpoints';
import type { ListResourcesData } from '../api/generated';

const resourceTypesQueryKey = ['resource-types'] as const;
const resourcesQueryKey = ['resources'] as const;

export interface MachineFilter {
  resourceTypeId: number | null;
  isActive: boolean | null;
}

export function useResourceTypes() {
  return useQuery({ queryKey: resourceTypesQueryKey, queryFn: listResourceTypes });
}

// The server filters and sorts (AC-30); the filter is only passed on.
export function useMachines(filter: MachineFilter) {
  return useQuery({
    queryKey: [...resourcesQueryKey, filter],
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

// A renamed or deleted type also changes what the machine list shows, so both are reloaded.
export async function reloadMachineData(queryClient: QueryClient): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: resourceTypesQueryKey }),
    queryClient.invalidateQueries({ queryKey: resourcesQueryKey }),
  ]);
}
