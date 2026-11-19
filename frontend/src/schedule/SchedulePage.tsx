import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useSignedInUser } from '../auth/signedInUser';
import { WithPlantTimeZone } from '../calendar/WithPlantTimeZone';
import { ConflictList } from './ConflictList';
import { PlacementForm } from './PlacementForm';
import type { PlacementTarget } from './placementTarget';
import { ScheduleTimeline } from './ScheduleTimeline';
import { addDays, DAYS_SHOWN, firstDayHolding, isPlantDate, plantToday, type TimelineZoom } from './timelineRange';
import { UnscheduledLotQueue } from './UnscheduledLotQueue';
import { useNow } from './useNow';
import { useTimelineView } from './useTimelineView';

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
  const timelineView = useTimelineView(firstDay, zoom);
  const [lotInForm, setLotInForm] = useState<PlacementTarget | null>(null);
  const [queueHidden, setQueueHidden] = useState(false);
  const openForm = canPlan ? setLotInForm : null;

  return (
    <>
      {lotInForm !== null && (
        <PlacementForm
          // A new key gives the form fresh state when another lot is chosen.
          key={lotInForm.lotId}
          target={lotInForm}
          renderConflictLink={timelineView.renderConflictLink}
          onClose={() => {
            setLotInForm(null);
          }}
        />
      )}
      <ScheduleTimeline view={timelineView} timeZone={timeZone} nowMs={nowMs} onMove={openForm} {...rangeActions} />
      <ConflictList onMove={openForm} renderConflictLink={timelineView.renderConflictLink} />
      <button
        type="button"
        className="queue-toggle"
        onClick={() => {
          setQueueHidden(!queueHidden);
        }}
      >
        {queueHidden ? 'Show queue' : 'Hide queue'}
      </button>
      {!queueHidden && <UnscheduledLotQueue onPlace={openForm} />}
    </>
  );
}
