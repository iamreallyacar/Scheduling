import { Link } from 'react-router';
import type { ConflictedPlacementResponse } from '../api/generated';
import { formatPlantTime } from '../calendar/plantClock';
import { WithPlantTimeZone } from '../calendar/WithPlantTimeZone';
import { QueryResult } from '../forms/QueryResult';
import type { PlacementTarget } from './PlacementForm';
import { useConflictedPlacements } from './scheduleQueries';
import { ViolationList, type ConflictLinkRenderer } from './ViolationList';

interface ConflictListProps {
  /** Opens the placement form; null when the user cannot plan. */
  onMove: ((target: PlacementTarget) => void) | null;
  renderConflictLink: ConflictLinkRenderer;
}

/** The placements that later facts made break a rule (F-SCH-4, AC-184). The server finds them; the page only lists them. */
export function ConflictList({ onMove, renderConflictLink }: ConflictListProps) {
  const conflictedPlacements = useConflictedPlacements();
  return (
    <QueryResult query={conflictedPlacements}>
      {(placements) => (
        <>
          <h2 id="conflicted-placements-heading">Conflicted placements ({placements.length})</h2>
          {placements.length === 0 ? (
            <p className="page-message">No placement breaks a rule.</p>
          ) : (
            <WithPlantTimeZone>
              {(timeZone) => <ConflictTable placements={placements} timeZone={timeZone} onMove={onMove} renderConflictLink={renderConflictLink} />}
            </WithPlantTimeZone>
          )}
        </>
      )}
    </QueryResult>
  );
}

function ConflictTable({ placements, timeZone, onMove, renderConflictLink }: ConflictListProps & { placements: ConflictedPlacementResponse[]; timeZone: string }) {
  return (
    <>
      <p>Times in {timeZone}.</p>
      <table aria-labelledby="conflicted-placements-heading">
        <thead>
          <tr>
            <th scope="col">Order</th>
            <th scope="col">Stage</th>
            <th scope="col">Lot</th>
            <th scope="col">Machine</th>
            <th scope="col">Planned start</th>
            <th scope="col">Planned end</th>
            <th scope="col">Broken rules</th>
            {onMove !== null && <th scope="col">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {placements.map((placement) => (
            <tr key={placement.placementId}>
              <td>
                <Link to={`/orders/${String(placement.orderId)}`}>{placement.orderNo}</Link>
              </td>
              <td>{`${String(placement.sequenceNo)} ${placement.resourceType.name}`}</td>
              <td>{placement.lotNo}</td>
              <td>{placement.resource.name}</td>
              <td>{formatPlantTime(placement.plannedStart, timeZone)}</td>
              <td>{formatPlantTime(placement.plannedEnd, timeZone)}</td>
              <td>
                <ViolationList violations={placement.violations} timeZone={timeZone} renderConflictLink={renderConflictLink} />
              </td>
              {onMove !== null && (
                <td>
                  <button
                    type="button"
                    onClick={() => {
                      onMove({
                        ...placement,
                        placement: { resourceId: placement.resource.id, plannedStart: placement.plannedStart },
                      });
                    }}
                  >
                    Move...
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
