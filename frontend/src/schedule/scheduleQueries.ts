import { useQuery } from '@tanstack/react-query';
import { getTimeline, listConflictedPlacements, listUnscheduledLots } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';

export function useUnscheduledLots() {
  return useQuery({ queryKey: queryKeys.unscheduledLots(SCHEDULE_VERSION), queryFn: () => listUnscheduledLots(SCHEDULE_VERSION) });
}

export function useConflictedPlacements() {
  return useQuery({ queryKey: queryKeys.conflictedPlacements(SCHEDULE_VERSION), queryFn: () => listConflictedPlacements(SCHEDULE_VERSION) });
}

export function useTimeline(range: { from: string; to: string }) {
  return useQuery({ queryKey: queryKeys.timeline(SCHEDULE_VERSION, range), queryFn: () => getTimeline(SCHEDULE_VERSION, range) });
}
