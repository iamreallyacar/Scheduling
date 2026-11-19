import { Link } from 'react-router';
import type { LotPredecessorResponse } from '../api/generated';
import { QueryResult } from '../forms/QueryResult';
import type { PlacementTarget } from './placementTarget';
import { useUnscheduledLots } from './scheduleQueries';

/** The lots that wait for placement, in the order the API returns them (F-SCH-7, AC-175). */
export function UnscheduledLotQueue({ onPlace }: { onPlace: ((target: PlacementTarget) => void) | null }) {
  const unscheduledLots = useUnscheduledLots();
  return (
    <QueryResult query={unscheduledLots}>
      {(lots) => (
        <>
          <h2 id="unscheduled-lots-heading">Unscheduled lots ({lots.length})</h2>
          {lots.length === 0 ? (
            <p className="page-message">Every lot is placed.</p>
          ) : (
            <table aria-labelledby="unscheduled-lots-heading">
              <thead>
                <tr>
                  <th scope="col">Order</th>
                  <th scope="col">Stage</th>
                  <th scope="col">Lot</th>
                  <th scope="col">Quantity</th>
                  <th scope="col">Duration (minutes)</th>
                  <th scope="col">Due</th>
                  <th scope="col">Depends on</th>
                  {onPlace !== null && <th scope="col">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {lots.map((lot) => (
                  <tr key={lot.id}>
                    <td>
                      <Link to={`/orders/${String(lot.orderId)}`}>{lot.orderNo}</Link>
                    </td>
                    <td>{`${String(lot.sequenceNo)} ${lot.resourceType.name}`}</td>
                    <td>{lot.lotNo}</td>
                    <td>{lot.quantity}</td>
                    <td>{lot.durationMinutes}</td>
                    <td>{lot.dueDate}</td>
                    <td>{lot.predecessors.length === 0 ? '-' : lot.predecessors.map(describePredecessor).join('; ')}</td>
                    {onPlace !== null && (
                      <td>
                        <button
                          type="button"
                          onClick={() => {
                            onPlace({ ...lot, lotId: lot.id, placement: null });
                          }}
                        >
                          Place...
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </QueryResult>
  );
}

function describePredecessor(predecessor: LotPredecessorResponse): string {
  return `${String(predecessor.sequenceNo)} ${predecessor.resourceType.name}, lag ${String(predecessor.minLagMinutes)} minutes`;
}
