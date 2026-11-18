import { useQuery } from '@tanstack/react-query';
import { listConflictedPlacements, listUnscheduledLots } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';

export function useUnscheduledLots() {
  return useQuery({ queryKey: queryKeys.unscheduledLots(SCHEDULE_VERSION), queryFn: () => listUnscheduledLots(SCHEDULE_VERSION) });
}

export function useConflictedPlacements() {
  return useQuery({ queryKey: queryKeys.conflictedPlacements(SCHEDULE_VERSION), queryFn: () => listConflictedPlacements(SCHEDULE_VERSION) });
}
