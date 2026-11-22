import type { ReactNode } from 'react';
import type { RuleViolation } from '../api/problem';
import { formatPlantTime } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';

/** Renders the link to a violation's conflicting item, or null when there is none to show (AC-191). */
export type ConflictLinkRenderer = (conflict: RuleViolation['conflict']) => ReactNode;

interface ViolationListProps {
  violations: readonly RuleViolation[];
  /** Absent where no view can show the conflicting items. */
  renderConflictLink?: ConflictLinkRenderer | undefined;
}

/** Every broken rule with its text and code, as the server returns them (R7, DEC-42). */
export function ViolationList({ violations, renderConflictLink }: ViolationListProps) {
  const timeZone = usePlantTimeZone();
  if (violations.length === 0) {
    return null;
  }
  return (
    <ul aria-label="Broken rules" className="field-errors">
      {violations.map((violation) => {
        const conflictLink = renderConflictLink?.(violation.conflict) ?? null;
        return (
          <li key={`${violation.code}-${violation.conflict.type}-${String(violation.conflict.id)}`}>
            {violation.detail}
            {violation.earliestAllowedStart !== null && ` Earliest allowed start: ${formatPlantTime(violation.earliestAllowedStart, timeZone)}.`}{' '}
            <code>{violation.code}</code>
            {conflictLink !== null && <> {conflictLink}</>}
          </li>
        );
      })}
    </ul>
  );
}
