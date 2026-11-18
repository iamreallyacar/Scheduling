import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
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
  const conflictsByPlacement = new Map((conflictedPlacements.data ?? []).map((conflict) => [conflict.placementId, conflict.violations]));
  const isDrawn = (bar: TimelineBar) =>
    (resourceTypeId === null || bar.resourceTypeId === resourceTypeId) && (!onlyConflicted || conflictsByPlacement.has(bar.placement.placementId));
  // A selected placement that the shown range or the filters no longer draw has no popover.
  const selectedBar =
    timeline.data === undefined ? undefined : barsOf(timeline.data).find((bar) => bar.placement.placementId === selectedPlacementId && isDrawn(bar));
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
      {selectedBar !== undefined && (
        <LotPopover
          bar={selectedBar}
          violations={conflictsByPlacement.get(selectedBar.placement.placementId) ?? []}
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
      <QueryResult query={timeline}>
        {(loadedTimeline) => (
          <TimelineChart
            timeline={loadedTimeline}
            firstDay={firstDay}
            zoom={zoom}
            timeZone={timeZone}
            nowMs={nowMs}
            resourceTypeId={resourceTypeId}
            conflictedPlacementIds={new Set(conflictsByPlacement.keys())}
            onlyConflicted={onlyConflicted}
            selectedPlacementId={selectedPlacementId}
            onSelect={setSelectedPlacementId}
          />
        )}
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
