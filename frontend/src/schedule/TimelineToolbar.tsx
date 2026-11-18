import { useId } from 'react';
import type { NamedReference } from '../api/generated';
import { formatPlantDate, type TimelineZoom } from './timelineRange';

interface TimelineToolbarProps {
  firstDay: string;
  lastDay: string;
  zoom: TimelineZoom;
  timeZone: string;
  resourceTypes: readonly NamedReference[];
  resourceTypeId: number | null;
  /** How many placements the conflict list holds; null until it is loaded. */
  conflictCount: number | null;
  onlyConflicted: boolean;
  onPrevious: () => void;
  onToday: () => void;
  onNext: () => void;
  onZoom: (zoom: TimelineZoom) => void;
  onResourceType: (resourceTypeId: number | null) => void;
  onOnlyConflicted: (onlyConflicted: boolean) => void;
}

const ALL_TYPES = '';

const ZOOM_LABELS: Readonly<Record<TimelineZoom, string>> = { day: 'Day', 'two-weeks': 'Two weeks' };

/** The range, zoom and filters of the timeline (AC-188, AC-189, wireframes.md section 2). */
export function TimelineToolbar(props: TimelineToolbarProps) {
  const { firstDay, lastDay, zoom, timeZone, resourceTypes, resourceTypeId, conflictCount, onlyConflicted } = props;
  const resourceTypeFieldId = useId();
  return (
    <div className="timeline-toolbar">
      <div className="form-actions">
        <button type="button" onClick={props.onPrevious}>
          {'< Prev'}
        </button>
        <button type="button" onClick={props.onToday}>
          Today
        </button>
        <button type="button" onClick={props.onNext}>
          {'Next >'}
        </button>
        <span>{firstDay === lastDay ? formatPlantDate(firstDay) : `${formatPlantDate(firstDay)} - ${formatPlantDate(lastDay)}`}</span>
        <span>Times in {timeZone}</span>
      </div>
      <div className="form-actions">
        <fieldset className="inline-fieldset">
          <legend>Zoom</legend>
          {(Object.keys(ZOOM_LABELS) as TimelineZoom[]).map((zoomOption) => (
            <label key={zoomOption}>
              <input
                type="radio"
                name="timeline-zoom"
                checked={zoom === zoomOption}
                onChange={() => {
                  props.onZoom(zoomOption);
                }}
              />{' '}
              {ZOOM_LABELS[zoomOption]}
            </label>
          ))}
        </fieldset>
        <label htmlFor={resourceTypeFieldId}>Resource type</label>
        <select
          id={resourceTypeFieldId}
          value={resourceTypeId === null ? ALL_TYPES : String(resourceTypeId)}
          onChange={(event) => {
            props.onResourceType(event.target.value === ALL_TYPES ? null : Number(event.target.value));
          }}
        >
          <option value={ALL_TYPES}>All</option>
          {resourceTypes.map((resourceType) => (
            <option key={resourceType.id} value={String(resourceType.id)}>
              {resourceType.name}
            </option>
          ))}
        </select>
        {conflictCount !== null && (
          <button
            type="button"
            aria-pressed={onlyConflicted}
            onClick={() => {
              props.onOnlyConflicted(!onlyConflicted);
            }}
          >
            {`! ${String(conflictCount)} conflicted placements`}
          </button>
        )}
      </div>
    </div>
  );
}
