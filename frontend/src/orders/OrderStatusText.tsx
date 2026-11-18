import type { OrderStatus } from '../api/generated';
import { ORDER_STATUS_LABELS } from './orderValues';

// Shows the status and late flag the server derived (F-ORD-2); it decides neither.
export function OrderStatusText({ status, isLate }: { status: OrderStatus; isLate: boolean }) {
  return (
    <>
      {ORDER_STATUS_LABELS[status]}
      {isLate && (
        <>
          {' '}
          <span className="late-mark">Late</span>
        </>
      )}
    </>
  );
}
