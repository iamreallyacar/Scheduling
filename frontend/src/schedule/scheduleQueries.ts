import { useQuery } from '@tanstack/react-query';
import type { GetEarliestSlotData, ProposeOrderAsapData } from '../api/generated';
import { getEarliestSlot, getTimeline, listConflictedPlacements, listUnscheduledLots, proposeOrderAsap } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';

export function useUnscheduledLots() {
  return useQuery({ queryKey: queryKeys.unscheduledLots(SCHEDULE_VERSION), queryFn: () => listUnscheduledLots(SCHEDULE_VERSION) });
}

export function useConflictedPlacements() {
  return useQuery({ queryKey: queryKeys.conflictedPlacements(SCHEDULE_VERSION), queryFn: () => listConflictedPlacements(SCHEDULE_VERSION) });
}

export type EarliestSlotSearch = NonNullable<GetEarliestSlotData['query']>;

// Asked only when the planner searches (refetch), and again on every search, because a search
// without notBefore starts at the server's now. An answer is dropped as soon as no form shows it,
// so a form opened again or a field typed back to an earlier value never shows an old suggestion.
export function useEarliestSlot(lotId: number, search: EarliestSlotSearch) {
  return useQuery({
    queryKey: queryKeys.earliestSlot(SCHEDULE_VERSION, lotId, search),
    queryFn: () => getEarliestSlot(SCHEDULE_VERSION, lotId, search),
    enabled: false,
    gcTime: 0,
  });
}

export type AsapProposalSearch = NonNullable<ProposeOrderAsapData['query']>;

// A read, though it is a POST (DEC-60), so it is a query (DEC-58). It is asked when the review opens
// and on [Recalculate], never on its own: the planner reviews this answer, and one from now would
// change as time passes. It is dropped as soon as no review shows it.
export function useAsapProposal(orderId: number, search: AsapProposalSearch) {
  return useQuery({
    queryKey: queryKeys.asapProposal(SCHEDULE_VERSION, orderId, search),
    queryFn: () => proposeOrderAsap(SCHEDULE_VERSION, orderId, search),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    gcTime: 0,
  });
}

export function useTimeline(range: { from: string; to: string }) {
  return useQuery({ queryKey: queryKeys.timeline(SCHEDULE_VERSION, range), queryFn: () => getTimeline(SCHEDULE_VERSION, range) });
}
