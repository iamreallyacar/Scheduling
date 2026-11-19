import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { placeLot, removeLotPlacement } from '../api/generated/endpoints.gen';
import type { NamedReference } from '../api/generated';
import { problemOf } from '../api/problem';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';
import { plantLocalToUtc, utcToPlantLocal } from '../calendar/plantClock';
import { WithPlantTimeZone } from '../calendar/WithPlantTimeZone';
import { SelectField, TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { QueryResult } from '../forms/QueryResult';
import { useMachines } from '../machines/machineQueries';
import { lotTitle } from './lotTitle';
import { ViolationList, type ConflictLinkRenderer } from './ViolationList';

/** The lot the form places, as the queue or the order page shows it. */
export interface PlacementTarget {
  lotId: number;
  orderNo: string;
  sequenceNo: number;
  resourceType: NamedReference;
  lotNo: number;
  quantity: number;
  durationMinutes: number;
  /** Its placement in the live version, or null when it has none. */
  placement: { resourceId: number; plannedStart: string } | null;
}

interface PlacementFormProps {
  target: PlacementTarget;
  onClose: () => void;
  /** Given where a view can show the conflicting items of a rejection (AC-191). */
  renderConflictLink?: ConflictLinkRenderer;
}

const NO_MACHINE_CHOSEN = '';

// F-SCH-1 (wireframes.md section 3). The server checks every rule and computes the planned end,
// so the form shows "Calculated when saved" (WQ1) and lists the rules a rejected save broke (R7).
export function PlacementForm(props: PlacementFormProps) {
  return <WithPlantTimeZone>{(timeZone) => <LoadedPlacementForm {...props} timeZone={timeZone} />}</WithPlantTimeZone>;
}

function LoadedPlacementForm({ target, timeZone, onClose, renderConflictLink }: PlacementFormProps & { timeZone: string }) {
  // The server filters the machines (AC-30); R1 is still checked when saving.
  const machines = useMachines({ resourceTypeId: target.resourceType.id, isActive: true });
  const [resourceId, setResourceId] = useState(target.placement === null ? NO_MACHINE_CHOSEN : String(target.placement.resourceId));
  const [start, setStart] = useState(target.placement === null ? '' : utcToPlantLocal(target.placement.plannedStart, timeZone));

  // Empty fields are sent as null, so the server reports what is missing.
  const saveMutation = useMutation({
    mutationFn: () =>
      placeLot(SCHEDULE_VERSION, target.lotId, {
        resourceId: resourceId === NO_MACHINE_CHOSEN ? null : Number(resourceId),
        plannedStart: start === '' ? null : plantLocalToUtc(start, timeZone),
      }),
    onSuccess: onClose,
  });
  const removeMutation = useMutation({ mutationFn: () => removeLotPlacement(SCHEDULE_VERSION, target.lotId), onSuccess: onClose });
  const problem = problemOf(saveMutation.error) ?? problemOf(removeMutation.error);
  const title = `${target.placement === null ? 'Place' : 'Move'} lot - ${lotTitle(target)}`;

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    removeMutation.reset();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label={title}>
      <h3>{title}</h3>
      <p>
        Quantity {target.quantity}, duration {target.durationMinutes} minutes of working time. Times in {timeZone}.
      </p>
      <ProblemSummary problem={problem} />
      {problem !== null && <ViolationList violations={problem.violations} timeZone={timeZone} renderConflictLink={renderConflictLink} />}
      <QueryResult query={machines}>
        {(loadedMachines) => (
          <SelectField
            label="Machine"
            name="resourceId"
            value={resourceId}
            onChange={setResourceId}
            problem={problem}
            options={[
              { value: NO_MACHINE_CHOSEN, label: 'Choose a machine' },
              ...loadedMachines.map((machine) => ({ value: String(machine.id), label: machine.name })),
            ]}
          />
        )}
      </QueryResult>
      <TextField label="Start" name="plannedStart" type="datetime-local" value={start} onChange={setStart} problem={problem} />
      <p>Planned end: Calculated when saved</p>
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save placement
        </button>
        {target.placement !== null && (
          <button
            type="button"
            disabled={removeMutation.isPending}
            onClick={() => {
              saveMutation.reset();
              removeMutation.mutate();
            }}
          >
            Remove placement
          </button>
        )}
        <button type="button" onClick={onClose}>
          Cancel
        </button>
      </div>
    </form>
  );
}
