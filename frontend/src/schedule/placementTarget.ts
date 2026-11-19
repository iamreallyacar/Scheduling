import type { NamedReference } from '../api/generated';

/** The lot the form places, as the queue or the order page shows it. */
export interface PlacementTarget {
  lotId: number;
  orderNo: string;
  sequenceNo: number;
  resourceType: NamedReference;
  lotNo: number;
  quantity: number;
  durationMinutes: number;
  /** Its placement in the live version, or null when it has none. */
  placement: { resourceId: number; plannedStart: string } | null;
}

/** The target for moving a lot that has a placement, as the timeline and the conflict list show it. */
export function placementTargetOf(placedLot: Omit<PlacementTarget, 'placement'> & { plannedStart: string }, resourceId: number): PlacementTarget {
  return { ...placedLot, placement: { resourceId, plannedStart: placedLot.plannedStart } };
}
