import { Link } from 'react-router';
import type { LotPredecessorResponse } from '../api/generated';
import { QueryResult } from '../forms/QueryResult';
import { useUnscheduledLots } from './scheduleQueries';

/** The schedule of the live version: for now the lots that wait for placement (F-SCH-7, wireframes.md section 2). */
export function SchedulePage() {
  const unscheduledLots = useUnscheduledLots();

  return (
    <section aria-labelledby="schedule-heading">
      <h1 id="schedule-heading">Schedule</h1>
      <p>Version: Live</p>
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
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </QueryResult>
    </section>
  );
}

function describePredecessor(predecessor: LotPredecessorResponse): string {
  return `${String(predecessor.sequenceNo)} ${predecessor.resourceType.name}, lag ${String(predecessor.minLagMinutes)} minutes`;
}
