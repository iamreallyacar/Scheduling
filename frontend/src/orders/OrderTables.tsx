import type { OrderResponse } from '../api/generated';
import { PlantTime } from '../calendar/PlantTime';
import type { PlacementTarget } from '../schedule/PlacementForm';
import { LOT_STATUS_LABELS } from './orderValues';

interface OrderTablesProps {
  order: OrderResponse;
  /** Opens the placement form for a lot; null when the user may not place lots. */
  onOpenPlacement: ((target: PlacementTarget) => void) | null;
}

/**
 * The generated operations, their lots with the status the server derived and their live
 * placement, and the dependencies between them (F-ORD-3, F-ORD-5, AC-71).
 */
export function OrderTables({ order, onOpenPlacement }: OrderTablesProps) {
  return (
    <>
      <h2 id="operations-heading">Operations</h2>
      <table aria-labelledby="operations-heading">
        <thead>
          <tr>
            <th scope="col">Stage</th>
            <th scope="col">Machine type</th>
            <th scope="col">Quantity</th>
            <th scope="col">Setup minutes</th>
            <th scope="col">Cycle minutes</th>
            <th scope="col">Units per cycle</th>
          </tr>
        </thead>
        <tbody>
          {order.operations.map((operation) => (
            <tr key={operation.id}>
              <th scope="row">{operation.sequenceNo}</th>
              <td>{operation.resourceType.name}</td>
              <td>{operation.quantity}</td>
              <td>{operation.setupMinutes}</td>
              <td>{operation.cycleMinutes}</td>
              <td>{operation.unitsPerCycle}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2 id="lots-heading">Lots</h2>
      <table aria-labelledby="lots-heading">
        <thead>
          <tr>
            <th scope="col">Stage</th>
            <th scope="col">Lot</th>
            <th scope="col">Quantity</th>
            <th scope="col">Duration (minutes)</th>
            <th scope="col">Status</th>
            <th scope="col">Machine</th>
            <th scope="col">Planned start</th>
            <th scope="col">Planned end</th>
            {onOpenPlacement !== null && <th scope="col">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {order.operations.flatMap((operation) =>
            operation.lots.map((lot) => (
              <tr key={lot.id}>
                <th scope="row">{operation.sequenceNo}</th>
                <td>{lot.lotNo}</td>
                <td>{lot.quantity}</td>
                <td>{lot.durationMinutes}</td>
                <td>{LOT_STATUS_LABELS[lot.status]}</td>
                <td>{lot.placement?.resource.name}</td>
                <td>{lot.placement !== null && <PlantTime value={lot.placement.plannedStart} />}</td>
                <td>{lot.placement !== null && <PlantTime value={lot.placement.plannedEnd} />}</td>
                {onOpenPlacement !== null && (
                  <td>
                    {/* Placed and unplaced lots only: a cancelled order's lots cannot be placed and a started lot stays where it is (R8). */}
                    {(lot.status === 'Unscheduled' || lot.status === 'Scheduled') && (
                      <button
                        type="button"
                        onClick={() => {
                          onOpenPlacement({
                            lotId: lot.id,
                            orderNo: order.orderNo,
                            sequenceNo: operation.sequenceNo,
                            resourceType: operation.resourceType,
                            lotNo: lot.lotNo,
                            quantity: lot.quantity,
                            durationMinutes: lot.durationMinutes,
                            placement: lot.placement === null ? null : { resourceId: lot.placement.resource.id, plannedStart: lot.placement.plannedStart },
                          });
                        }}
                      >
                        {lot.placement === null ? 'Place...' : 'Move...'}
                      </button>
                    )}
                  </td>
                )}
              </tr>
            )),
          )}
        </tbody>
      </table>

      <h2 id="dependencies-heading">Dependencies</h2>
      {order.dependencies.length === 0 ? (
        <p className="page-message">The stages do not wait for each other.</p>
      ) : (
        <table aria-labelledby="dependencies-heading">
          <thead>
            <tr>
              <th scope="col">Predecessor stage</th>
              <th scope="col">Successor stage</th>
              <th scope="col">Minimum lag (minutes)</th>
            </tr>
          </thead>
          <tbody>
            {order.dependencies.map((dependency) => (
              <tr key={`${String(dependency.predecessorSequenceNo)}-${String(dependency.successorSequenceNo)}`}>
                <td>{dependency.predecessorSequenceNo}</td>
                <td>{dependency.successorSequenceNo}</td>
                <td>{dependency.minLagMinutes}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
