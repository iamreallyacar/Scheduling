import { Link } from 'react-router';
import type { TimelineResponse } from '../api/generated';
import { barsOf } from './timelineBars';
import type { ConflictLinkRenderer } from './ViolationList';

/**
 * Links a violation to its conflicting item when the shown timeline holds it (AC-191, DEC-46): a
 * machine and an unavailability to the machine page, a placement and a lot to that bar.
 */
export function conflictLinksOn(timeline: TimelineResponse | undefined, onShowPlacement: (placementId: number) => void): ConflictLinkRenderer {
  if (timeline === undefined) {
    return () => null;
  }
  const resources = timeline.resourceTypes.flatMap((resourceType) => resourceType.resources);
  const bars = barsOf(timeline);
  const showBarButton = (placementId: number, label: string) => (
    <button
      type="button"
      className="link-button"
      onClick={() => {
        onShowPlacement(placementId);
      }}
    >
      {label}
    </button>
  );

  return (conflict) => {
    switch (conflict.type) {
      case 'resource':
        return resources.some((resource) => resource.id === conflict.id) ? <Link to={`/machines/${String(conflict.id)}`}>Show machine</Link> : null;
      case 'unavailability': {
        const owner = resources.find((resource) => resource.unavailabilities.some((unavailability) => unavailability.id === conflict.id));
        return owner === undefined ? null : <Link to={`/machines/${String(owner.id)}`}>Show unavailability</Link>;
      }
      case 'placement': {
        const bar = bars.find((candidate) => candidate.placement.placementId === conflict.id);
        return bar === undefined ? null : showBarButton(bar.placement.placementId, 'Show placement');
      }
      case 'lot': {
        const bar = bars.find((candidate) => candidate.placement.lotId === conflict.id);
        return bar === undefined ? null : showBarButton(bar.placement.placementId, 'Show lot');
      }
      default:
        throw new Error(`The API named a conflict of unknown type "${conflict.type}".`);
    }
  };
}
