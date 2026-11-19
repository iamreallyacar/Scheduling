import { Link } from 'react-router';
import type { RuleViolation } from '../api/problem';
import { LOT_STATUS_LABELS } from '../orders/orderValues';
import { formatPlantTime } from '../calendar/plantClock';
import type { TimelineBar } from './timelineBars';
import { ViolationList, type ConflictLinkRenderer } from './ViolationList';

interface LotPopoverProps {
  bar: TimelineBar;
  /** The violations the conflict list holds for the placement, empty when it holds none (DEC-46); null until the list has loaded (DEC-52). */
  violations: readonly RuleViolation[] | null;
  timeZone: string;
  renderConflictLink: ConflictLinkRenderer;
  /** Opens the placement form; null when the user cannot plan. */
  onMove: (() => void) | null;
  onClose: () => void;
}

/** A bar's lot as the API returns it (AC-190). Removal goes through the placement form (DEC-46). */
export function LotPopover({ bar, violations, timeZone, renderConflictLink, onMove, onClose }: LotPopoverProps) {
  const { placement, resource } = bar;
  const title = `Order ${placement.orderNo}, stage ${String(placement.sequenceNo)} ${placement.resourceType.name}, lot ${String(placement.lotNo)}`;
  return (
    <section role="dialog" aria-label={title} className="panel timeline-popover">
      <div className="popover-heading">
        <h3>{title}</h3>
        <button type="button" aria-label="Close" onClick={onClose}>
          x
        </button>
      </div>
      <dl className="popover-facts">
        <dt>Product</dt>
        <dd>{`${placement.product.code} ${placement.product.name}`}</dd>
        <dt>Quantity</dt>
        <dd>{placement.quantity}</dd>
        <dt>Machine</dt>
        <dd>{resource.name}</dd>
        <dt>Planned</dt>
        <dd>{`${formatPlantTime(placement.plannedStart, timeZone)} - ${formatPlantTime(placement.plannedEnd, timeZone)}`}</dd>
        <dt>Status</dt>
        <dd>{`${LOT_STATUS_LABELS[placement.status]}, order ${placement.orderIsLate ? 'late' : 'on time'}`}</dd>
      </dl>
      {violations === null ? (
        <p className="page-message">The broken rules are not known until the conflict list has loaded.</p>
      ) : (
        <ViolationList violations={violations} timeZone={timeZone} renderConflictLink={renderConflictLink} />
      )}
      <div className="form-actions">
        {onMove !== null && (
          <button type="button" onClick={onMove}>
            Move...
          </button>
        )}
        <Link to={`/orders/${String(placement.orderId)}`}>Open order</Link>
      </div>
    </section>
  );
}
