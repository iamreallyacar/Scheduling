import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import type { TimelineResponse } from '../api/generated';
import type { RuleViolation } from '../api/problem';
import { useSignedInUser } from '../auth/signedInUser';
import { WithPlantTimeZone } from '../calendar/WithPlantTimeZone';
import { QueryResult } from '../forms/QueryResult';
import { ConflictList } from './ConflictList';
import { conflictLinksOn } from './conflictLinks';
import { LotPopover } from './LotPopover';
import { PlacementForm, type PlacementTarget } from './PlacementForm';
import { useConflictedPlacements, useTimeline } from './scheduleQueries';
import { barsOf, type TimelineBar } from './timelineBars';
import { TimelineChart } from './TimelineChart';
import { addDays, DAYS_SHOWN, firstDayHolding, isPlantDate, lastShownDay, plantToday, type TimelineZoom } from './timelineRange';
import { TimelineToolbar } from './TimelineToolbar';
import { UnscheduledLotQueue } from './UnscheduledLotQueue';
import { useNow } from './useNow';

// The shown range lives in the address, so it survives opening an order and coming back.
const FIRST_DAY_PARAMETER = 'from';
const ZOOM_PARAMETER = 'zoom';
const DAY_ZOOM = 'day';
// A placement the loaded conflict list does not hold breaks no rule.
const NO_VIOLATIONS: readonly RuleViolation[] = [];

/** The schedule of the live version: the timeline (F-SCH-2), the conflicted placements (F-SCH-4) and the lots that wait for placement (F-SCH-7), as in wireframes.md section 2. A Planner places and moves them (F-SCH-1). */
export function SchedulePage() {
  return (
    <section aria-labelledby="schedule-heading">
      <h1 id="schedule-heading">Schedule</h1>
      <p>Version: Live</p>
      <WithPlantTimeZone>{(timeZone) => <ScheduleOfRange timeZone={timeZone} />}</WithPlantTimeZone>
    </section>
  );
}

function ScheduleOfRange({ timeZone }: { timeZone: string }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const nowMs = useNow();
  const firstDayParameter = searchParams.get(FIRST_DAY_PARAMETER);
  const zoomParameter = searchParams.get(ZOOM_PARAMETER);
  if ((firstDayParameter !== null && !isPlantDate(firstDayParameter)) || (zoomParameter !== null && zoomParameter !== DAY_ZOOM)) {
    return (
      <p className="problem" role="alert">
        The address names no range the timeline can show. <Link to="/schedule">Show this week</Link>
      </p>
    );
  }

  const zoom: TimelineZoom = zoomParameter === DAY_ZOOM ? 'day' : 'two-weeks';
  // Without a first day the timeline shows the range that holds today (A44).
  const firstDay = firstDayParameter ?? firstDayHolding(plantToday(timeZone, new Date(nowMs)), zoom);
  const showRange = (shownFirstDay: string, shownZoom: TimelineZoom) => {
    setSearchParams(shownZoom === 'day' ? { [FIRST_DAY_PARAMETER]: shownFirstDay, [ZOOM_PARAMETER]: DAY_ZOOM } : { [FIRST_DAY_PARAMETER]: shownFirstDay });
  };

  return (
    <ScheduleContent
      timeZone={timeZone}
      nowMs={nowMs}
      firstDay={firstDay}
      zoom={zoom}
      onPrevious={() => {
        showRange(addDays(firstDay, -DAYS_SHOWN[zoom]), zoom);
      }}
      onToday={() => {
        showRange(firstDayHolding(plantToday(timeZone, new Date()), zoom), zoom);
      }}
      onNext={() => {
        showRange(addDays(firstDay, DAYS_SHOWN[zoom]), zoom);
      }}
      onZoom={(shownZoom) => {
        showRange(firstDay, shownZoom);
      }}
    />
  );
}

interface ScheduleContentProps {
  timeZone: string;
  nowMs: number;
  firstDay: string;
  zoom: TimelineZoom;
  onPrevious: () => void;
  onToday: () => void;
  onNext: () => void;
  onZoom: (zoom: TimelineZoom) => void;
}

function ScheduleContent({ timeZone, nowMs, firstDay, zoom, ...rangeActions }: ScheduleContentProps) {
  const { canPlan } = useSignedInUser();
  const lastDay = lastShownDay(firstDay, zoom);
  const timeline = useTimeline({ from: firstDay, to: lastDay });
  const conflictedPlacements = useConflictedPlacements();
  const [lotInForm, setLotInForm] = useState<PlacementTarget | null>(null);
  const [selectedPlacementId, setSelectedPlacementId] = useState<number | null>(null);
  const [resourceTypeId, setResourceTypeId] = useState<number | null>(null);
  const [onlyConflicted, setOnlyConflicted] = useState(false);
  const [queueHidden, setQueueHidden] = useState(false);

  // The item may be hidden by a filter, so a link clears them before it opens the bar.
  const renderConflictLink = conflictLinksOn(timeline.data, (placementId) => {
    setResourceTypeId(null);
    setOnlyConflicted(false);
    setSelectedPlacementId(placementId);
  });
  // Null until the conflict list has loaded: a list that is loading or failed is unknown, not empty (DEC-52).
  const conflictsByPlacement: ReadonlyMap<number, readonly RuleViolation[]> | null =
    conflictedPlacements.data === undefined ? null : new Map(conflictedPlacements.data.map((conflict) => [conflict.placementId, conflict.violations]));
  // Without a loaded list no bar is marked, and the notice above the chart says why.
  const conflictedPlacementIds: ReadonlySet<number> = new Set(conflictsByPlacement?.keys());
  const moveTargetOf = ({ placement, resource }: TimelineBar): PlacementTarget => ({
    ...placement,
    placement: { resourceId: resource.id, plannedStart: placement.plannedStart },
  });

  return (
    <>
      {lotInForm !== null && (
        <PlacementForm
          // A new key gives the form fresh state when another lot is chosen.
          key={lotInForm.lotId}
          target={lotInForm}
          renderConflictLink={renderConflictLink}
          onClose={() => {
            setLotInForm(null);
          }}
        />
      )}
      <h2>Timeline</h2>
      <TimelineToolbar
        firstDay={firstDay}
        lastDay={lastDay}
        zoom={zoom}
        timeZone={timeZone}
        resourceTypes={timeline.data?.resourceTypes ?? []}
        resourceTypeId={resourceTypeId}
        conflictCount={conflictedPlacements.data?.length ?? null}
        onlyConflicted={onlyConflicted}
        onResourceType={setResourceTypeId}
        onOnlyConflicted={setOnlyConflicted}
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
                  timeZone={timeZone}
                  renderConflictLink={renderConflictLink}
                  onMove={
                    canPlan
                      ? () => {
                          setLotInForm(moveTargetOf(selectedBar));
                        }
                      : null
                  }
                  onClose={() => {
                    setSelectedPlacementId(null);
                  }}
                />
              )}
              <TimelineChart
                timeline={shownTimeline}
                firstDay={firstDay}
                zoom={zoom}
                timeZone={timeZone}
                nowMs={nowMs}
                conflictedPlacementIds={conflictedPlacementIds}
                selectedPlacementId={selectedPlacementId}
                onSelect={setSelectedPlacementId}
              />
            </>
          );
        }}
      </QueryResult>
      <ConflictList onMove={canPlan ? setLotInForm : null} renderConflictLink={renderConflictLink} />
      <button
        type="button"
        className="queue-toggle"
        onClick={() => {
          setQueueHidden(!queueHidden);
        }}
      >
        {queueHidden ? 'Show queue' : 'Hide queue'}
      </button>
      {!queueHidden && <UnscheduledLotQueue onPlace={canPlan ? setLotInForm : null} />}
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
