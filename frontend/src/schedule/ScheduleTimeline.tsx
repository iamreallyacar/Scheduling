import type { TimelineResponse } from '../api/generated';
import type { RuleViolation } from '../api/problem';
import { QueryResult } from '../forms/QueryResult';
import { LotPopover } from './LotPopover';
import { placementTargetOf, type PlacementTarget } from './placementTarget';
import { barsOf } from './timelineBars';
import { TimelineChart } from './TimelineChart';
import type { TimelineZoom } from './timelineRange';
import { TimelineToolbar } from './TimelineToolbar';
import type { TimelineView } from './useTimelineView';

// A placement the loaded conflict list does not hold breaks no rule.
const NO_VIOLATIONS: readonly RuleViolation[] = [];

interface ScheduleTimelineProps {
  view: TimelineView;
  nowMs: number;
  /** Opens the placement form; null when the user cannot plan. */
  onMove: ((target: PlacementTarget) => void) | null;
  onPrevious: () => void;
  onToday: () => void;
  onNext: () => void;
  onZoom: (zoom: TimelineZoom) => void;
}

/** The timeline with its toolbar, the conflict marker notice and the popover of the selected bar (F-SCH-2). */
export function ScheduleTimeline({ view, nowMs, onMove, ...rangeActions }: ScheduleTimelineProps) {
  const { firstDay, lastDay, zoom, timeline, conflictedPlacements, selectedPlacementId, resourceTypeId, onlyConflicted, renderConflictLink } = view;
  // Null until the conflict list has loaded: a list that is loading or failed is unknown, not empty (DEC-52).
  const conflictsByPlacement: ReadonlyMap<number, readonly RuleViolation[]> | null =
    conflictedPlacements.data === undefined ? null : new Map(conflictedPlacements.data.map((conflict) => [conflict.placementId, conflict.violations]));
  // Without a loaded list no bar is marked, and the notice above the chart says why.
  const conflictedPlacementIds: ReadonlySet<number> = new Set(conflictsByPlacement?.keys());

  return (
    <>
      <h2>Timeline</h2>
      <TimelineToolbar
        firstDay={firstDay}
        lastDay={lastDay}
        zoom={zoom}
        resourceTypes={timeline.data?.resourceTypes ?? []}
        resourceTypeId={resourceTypeId}
        conflictCount={conflictedPlacements.data?.length ?? null}
        onlyConflicted={onlyConflicted}
        onResourceType={view.setResourceTypeId}
        onOnlyConflicted={view.setOnlyConflicted}
        {...rangeActions}
      />
      {conflictsByPlacement === null &&
        (conflictedPlacements.isError ? (
          <p className="problem" role="alert">
            The conflict markers could not be loaded, so no bar is marked as conflicted.
          </p>
        ) : (
          <p className="page-message" role="status">
            Loading the conflict markers…
          </p>
        ))}
      <QueryResult query={timeline}>
        {(loadedTimeline) => {
          const shownTimeline = timelineShownBy(loadedTimeline, resourceTypeId, onlyConflicted && conflictsByPlacement !== null ? conflictedPlacementIds : null);
          // A selected placement that the shown range or the filters no longer draw has no popover.
          const selectedBar = barsOf(shownTimeline).find((bar) => bar.placement.placementId === selectedPlacementId);
          return (
            <>
              {selectedBar !== undefined && (
                <LotPopover
                  bar={selectedBar}
                  violations={conflictsByPlacement === null ? null : (conflictsByPlacement.get(selectedBar.placement.placementId) ?? NO_VIOLATIONS)}
                  renderConflictLink={renderConflictLink}
                  onMove={
                    onMove === null
                      ? null
                      : () => {
                          onMove(placementTargetOf(selectedBar.placement, selectedBar.resource.id));
                        }
                  }
                  onClose={() => {
                    view.setSelectedPlacementId(null);
                  }}
                />
              )}
              <TimelineChart
                timeline={shownTimeline}
                firstDay={firstDay}
                zoom={zoom}
                nowMs={nowMs}
                conflictedPlacementIds={conflictedPlacementIds}
                selectedPlacementId={selectedPlacementId}
                onSelect={view.setSelectedPlacementId}
              />
            </>
          );
        }}
      </QueryResult>
    </>
  );
}

// The toolbar's filters, applied once: the chart draws the result and the popover opens only on a bar in
// it. A null resource type shows every type; null placement ids show every bar.
function timelineShownBy(timeline: TimelineResponse, resourceTypeId: number | null, onlyPlacementIds: ReadonlySet<number> | null): TimelineResponse {
  return {
    ...timeline,
    resourceTypes: timeline.resourceTypes
      .filter((resourceType) => resourceTypeId === null || resourceType.id === resourceTypeId)
      .map((resourceType) => ({
        ...resourceType,
        resources: resourceType.resources.map((resource) => ({
          ...resource,
          placements: resource.placements.filter((placement) => onlyPlacementIds === null || onlyPlacementIds.has(placement.placementId)),
        })),
      })),
  };
}
