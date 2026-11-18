import { useState } from 'react';
import { Link } from 'react-router';
import type { LotPredecessorResponse, UnscheduledLotResponse } from '../api/generated';
import { useSignedInUser } from '../auth/signedInUser';
import { QueryResult } from '../forms/QueryResult';
import { PlacementForm } from './PlacementForm';
import { useUnscheduledLots } from './scheduleQueries';

/** The schedule of the live version: for now the lots that wait for placement (F-SCH-7, wireframes.md section 2). A Planner places them (F-SCH-1). */
export function SchedulePage() {
  const unscheduledLots = useUnscheduledLots();
  const { canPlan } = useSignedInUser();
  const [lotInForm, setLotInForm] = useState<UnscheduledLotResponse | null>(null);

  return (
    <section aria-labelledby="schedule-heading">
      <h1 id="schedule-heading">Schedule</h1>
      <p>Version: Live</p>
      {lotInForm !== null && (
        <PlacementForm
          // A new key gives the form fresh state when another lot is chosen.
          key={lotInForm.id}
          target={{ ...lotInForm, lotId: lotInForm.id, placement: null }}
          onClose={() => {
            setLotInForm(null);
          }}
        />
      )}
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
                    {canPlan && <th scope="col">Actions</th>}
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
                      {canPlan && (
                        <td>
                          <button
                            type="button"
                            onClick={() => {
                              setLotInForm(lot);
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
    </section>
  );
}

function describePredecessor(predecessor: LotPredecessorResponse): string {
  return `${String(predecessor.sequenceNo)} ${predecessor.resourceType.name}, lag ${String(predecessor.minLagMinutes)} minutes`;
}
