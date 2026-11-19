import type { TimelineResourceResponse, TimelineResponse } from '../api/generated';
import { formatPlantTime, plantLocalToUtc } from '../calendar/plantClock';
import { RESOURCE_STATUS_LABELS, UNAVAILABILITY_KIND_LABELS } from '../machines/machineValues';
import { addDays, DAYS_SHOWN, type TimelineZoom } from './timelineRange';

interface TimelineChartProps {
  timeline: TimelineResponse;
  firstDay: string;
  zoom: TimelineZoom;
  timeZone: string;
  nowMs: number;
  /** The placements drawn as conflicted (DEC-46). */
  conflictedPlacementIds: ReadonlySet<number>;
  selectedPlacementId: number | null;
  onSelect: (placementId: number) => void;
}

interface ShownRange {
  startMs: number;
  endMs: number;
}

const HOURS_BETWEEN_DAY_TICKS = 6;

// F-SCH-2, read-only (wireframes.md section 2): what the API returns, drawn on the shown range. The
// chart decides no rule and filters nothing; it draws every machine and bar it is given (AC-189), so
// the caller decides what is shown.
export function TimelineChart({ timeline, firstDay, zoom, timeZone, nowMs, conflictedPlacementIds, selectedPlacementId, onSelect }: TimelineChartProps) {
  const range: ShownRange = { startMs: Date.parse(timeline.start), endMs: Date.parse(timeline.end) };
  const nowIsShown = range.startMs <= nowMs && nowMs < range.endMs;

  return (
    <table aria-label="Timeline" className="timeline">
      <thead>
        <tr>
          <th scope="col">Machine</th>
          <th scope="col">Status</th>
          <th scope="col" className="timeline-scale-cell">
            <div className="timeline-scale">
              {ticksOf(firstDay, zoom, timeZone).map((tick) => (
                <span key={tick.local} className="timeline-tick" style={{ left: spanStyle(range, Date.parse(tick.utc), range.endMs).left }}>
                  {tick.label}
                </span>
              ))}
              {nowIsShown && <div role="separator" aria-label="Now" className="timeline-now" style={{ left: spanStyle(range, nowMs, range.endMs).left }} />}
            </div>
          </th>
        </tr>
      </thead>
      {timeline.resourceTypes.map((resourceType) => (
        <tbody key={resourceType.id}>
          <tr>
            <th scope="rowgroup" colSpan={3} className="timeline-group">
              {resourceType.name}
            </th>
          </tr>
          {resourceType.resources.map((resource) => (
            <MachineRow
              key={resource.id}
              resource={resource}
              range={range}
              nowLineMs={nowIsShown ? nowMs : null}
              timeZone={timeZone}
              conflictedPlacementIds={conflictedPlacementIds}
              selectedPlacementId={selectedPlacementId}
              onSelect={onSelect}
            />
          ))}
        </tbody>
      ))}
    </table>
  );
}

interface MachineRowProps {
  resource: TimelineResourceResponse;
  range: ShownRange;
  nowLineMs: number | null;
  timeZone: string;
  conflictedPlacementIds: ReadonlySet<number>;
  selectedPlacementId: number | null;
  onSelect: (placementId: number) => void;
}

function MachineRow({ resource, range, nowLineMs, timeZone, conflictedPlacementIds, selectedPlacementId, onSelect }: MachineRowProps) {
  return (
    <tr>
      <th scope="row">{resource.isActive ? resource.name : `${resource.name} (inactive)`}</th>
      <td>{RESOURCE_STATUS_LABELS[resource.status]}</td>
      <td className="timeline-lane-cell">
        <div className="timeline-lane">
          {nonWorkingSpans(resource, range).map(([startMs, endMs]) => (
            <div key={startMs} title="Non-working time" className="timeline-off" style={spanStyle(range, startMs, endMs)} />
          ))}
          {resource.unavailabilities.map((unavailability) => (
            <div
              key={unavailability.id}
              className={`timeline-unavailability timeline-${unavailability.kind.toLowerCase()}`}
              style={spanStyle(range, Date.parse(unavailability.start), unavailability.end === null ? range.endMs : Date.parse(unavailability.end))}
              title={unavailability.reason ?? undefined}
            >
              {UNAVAILABILITY_KIND_LABELS[unavailability.kind]}
              {unavailability.end === null && ', no end'}
            </div>
          ))}
          {nowLineMs !== null && <div aria-hidden="true" className="timeline-now" style={{ left: spanStyle(range, nowLineMs, range.endMs).left }} />}
          {resource.placements.map((placement) => {
            const isConflicted = conflictedPlacementIds.has(placement.placementId);
            return (
              <button
                key={placement.placementId}
                type="button"
                className={isConflicted ? 'timeline-bar timeline-conflicted' : 'timeline-bar'}
                style={spanStyle(range, Date.parse(placement.plannedStart), Date.parse(placement.plannedEnd))}
                title={`${formatPlantTime(placement.plannedStart, timeZone)} - ${formatPlantTime(placement.plannedEnd, timeZone)}`}
                aria-pressed={placement.placementId === selectedPlacementId}
                onClick={() => {
                  onSelect(placement.placementId);
                }}
              >
                {`${isConflicted ? '! ' : ''}${placement.orderNo}/L${String(placement.lotNo)}`}
              </button>
            );
          })}
        </div>
      </td>
    </tr>
  );
}

// The left edge and width of a span as shares of the shown range, cut at its edges.
function spanStyle(range: ShownRange, startMs: number, endMs: number): { left: string; width: string } {
  const length = range.endMs - range.startMs;
  const shownStart = Math.max(startMs, range.startMs);
  const shownEnd = Math.min(endMs, range.endMs);
  return { left: percentOf((shownStart - range.startMs) / length), width: percentOf((shownEnd - shownStart) / length) };
}

function percentOf(share: number): string {
  return `${(share * 100).toFixed(3)}%`;
}

// The time between the working periods the API returns, which are ordered and do not overlap (DEC-45).
function nonWorkingSpans(resource: TimelineResourceResponse, range: ShownRange): [number, number][] {
  const spans: [number, number][] = [];
  let workedUntil = range.startMs;
  for (const period of resource.workingPeriods) {
    const periodStart = Date.parse(period.start);
    if (periodStart > workedUntil) {
      spans.push([workedUntil, periodStart]);
    }
    workedUntil = Math.max(workedUntil, Date.parse(period.end));
  }
  if (workedUntil < range.endMs) {
    spans.push([workedUntil, range.endMs]);
  }
  return spans;
}

// One tick a day for two weeks, and every few hours for one day, at their instants on the plant clock.
function ticksOf(firstDay: string, zoom: TimelineZoom, timeZone: string): { local: string; utc: string; label: string }[] {
  if (zoom === 'two-weeks') {
    return Array.from({ length: DAYS_SHOWN[zoom] }, (_, dayOffset) => addDays(firstDay, dayOffset)).map((day) => {
      const local = `${day}T00:00`;
      const utc = plantLocalToUtc(local, timeZone);
      return { local, utc, label: new Intl.DateTimeFormat('en-GB', { timeZone, weekday: 'short', day: 'numeric' }).format(new Date(utc)) };
    });
  }
  return Array.from({ length: 24 / HOURS_BETWEEN_DAY_TICKS }, (_, tickIndex) => {
    const hour = String(tickIndex * HOURS_BETWEEN_DAY_TICKS).padStart(2, '0');
    const local = `${firstDay}T${hour}:00`;
    return { local, utc: plantLocalToUtc(local, timeZone), label: `${hour}:00` };
  });
}
