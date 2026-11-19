import type { TimelinePlacementResponse, TimelineResponse } from '../api/generated';

/** A placement on the timeline with the machine row it is on. */
export interface TimelineBar {
  placement: TimelinePlacementResponse;
  resource: { id: number; name: string };
}

export function barsOf(timeline: TimelineResponse): TimelineBar[] {
  return timeline.resourceTypes.flatMap((resourceType) =>
    resourceType.resources.flatMap((resource) => resource.placements.map((placement) => ({ placement, resource: { id: resource.id, name: resource.name } }))),
  );
}
