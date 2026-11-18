import type { RuleViolation } from '../api/problem';
import { formatPlantTime } from '../calendar/plantClock';

/** Every broken rule with its text and code, as the server returns them (R7, DEC-42). */
export function ViolationList({ violations, timeZone }: { violations: readonly RuleViolation[]; timeZone: string }) {
  if (violations.length === 0) {
    return null;
  }
  return (
    <ul aria-label="Broken rules" className="field-errors">
      {violations.map((violation) => (
        <li key={`${violation.code}-${violation.conflict.type}-${String(violation.conflict.id)}`}>
          {violation.detail}
          {violation.earliestAllowedStart !== null && ` Earliest allowed start: ${formatPlantTime(violation.earliestAllowedStart, timeZone)}.`}{' '}
          <code>{violation.code}</code>
        </li>
      ))}
    </ul>
  );
}
