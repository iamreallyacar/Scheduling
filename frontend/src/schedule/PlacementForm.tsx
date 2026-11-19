import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { placeLot, removeLotPlacement } from '../api/generated/endpoints.gen';
import { problemOf } from '../api/problem';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';
import { plantLocalToUtc, utcToPlantLocal } from '../calendar/plantClock';
import { WithPlantTimeZone } from '../calendar/WithPlantTimeZone';
import { SelectField, TextField } from '../forms/Fields';
import { NOTHING_CHOSEN, numberOrNullIfEmpty } from '../forms/fieldValues';
import { FormPanel } from '../forms/FormPanel';
import { QueryResult } from '../forms/QueryResult';
import { useMachines } from '../machines/machineQueries';
import { EarliestSlotFinder } from './EarliestSlotFinder';
import { lotTitle } from './lotTitle';
import type { PlacementTarget } from './placementTarget';
import { ViolationList, type ConflictLinkRenderer } from './ViolationList';

interface PlacementFormProps {
  target: PlacementTarget;
  onClose: () => void;
  /** Given where a view can show the conflicting items of a rejection (AC-191). */
  renderConflictLink?: ConflictLinkRenderer;
}

// F-SCH-1 and F-SCH-11 (wireframes.md section 3). The server checks every rule and computes the
// planned end, so the form shows "Calculated when saved" (WQ1) and lists the rules a rejected save
// broke (R7). A suggested slot only fills the Placement part; the planner still saves it.
export function PlacementForm(props: PlacementFormProps) {
  return <WithPlantTimeZone>{(timeZone) => <LoadedPlacementForm {...props} timeZone={timeZone} />}</WithPlantTimeZone>;
}

function LoadedPlacementForm({ target, timeZone, onClose, renderConflictLink }: PlacementFormProps & { timeZone: string }) {
  // The server filters the machines (AC-30); R1 is still checked when saving.
  const machines = useMachines({ resourceTypeId: target.resourceType.id, isActive: true });
  const [resourceId, setResourceId] = useState(target.placement === null ? NOTHING_CHOSEN : String(target.placement.resourceId));
  const [start, setStart] = useState(target.placement === null ? '' : utcToPlantLocal(target.placement.plannedStart, timeZone));

  // Empty fields are sent as null, so the server reports what is missing.
  const saveMutation = useMutation({
    mutationFn: () =>
      placeLot(SCHEDULE_VERSION, target.lotId, {
        resourceId: numberOrNullIfEmpty(resourceId),
        plannedStart: start === '' ? null : plantLocalToUtc(start, timeZone),
      }),
    onSuccess: onClose,
  });
  const removeMutation = useMutation({ mutationFn: () => removeLotPlacement(SCHEDULE_VERSION, target.lotId), onSuccess: onClose });
  const problem = problemOf(saveMutation.error) ?? problemOf(removeMutation.error);
  const title = `${target.placement === null ? 'Place' : 'Move'} lot - ${lotTitle(target)}`;

  return (
    <FormPanel
      accessibleName={title}
      heading={title}
      description={
        <p>
          Quantity {target.quantity}, duration {target.durationMinutes} minutes of working time. Times in {timeZone}.
        </p>
      }
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save placement"
      onSubmit={() => {
        removeMutation.reset();
        saveMutation.mutate();
      }}
      extraActions={
        target.placement !== null && (
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
        )
      }
      onCancel={onClose}
    >
      {problem !== null && <ViolationList violations={problem.violations} timeZone={timeZone} renderConflictLink={renderConflictLink} />}
      <QueryResult query={machines}>
        {(loadedMachines) => (
          <EarliestSlotFinder
            target={target}
            machines={loadedMachines}
            timeZone={timeZone}
            onUse={(slot) => {
              setResourceId(String(slot.resource.id));
              setStart(utcToPlantLocal(slot.plannedStart, timeZone));
            }}
          />
        )}
      </QueryResult>
      <fieldset>
        <legend>Placement</legend>
        <QueryResult query={machines}>
          {(loadedMachines) => (
            <SelectField
              label="Machine"
              name="resourceId"
              value={resourceId}
              onChange={setResourceId}
              problem={problem}
              options={[
                { value: NOTHING_CHOSEN, label: 'Choose a machine' },
                ...loadedMachines.map((machine) => ({ value: String(machine.id), label: machine.name })),
              ]}
            />
          )}
        </QueryResult>
        <TextField label="Start" name="plannedStart" type="datetime-local" value={start} onChange={setStart} problem={problem} />
        <p>Planned end: Calculated when saved</p>
      </fieldset>
    </FormPanel>
  );
}
