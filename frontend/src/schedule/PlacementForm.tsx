import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { placeLot, removeLotPlacement } from '../api/generated/endpoints.gen';
import { problemOf, type ApiProblem } from '../api/problem';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';
import { utcToPlantLocal } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { START_GRID_STEP_SECONDS, SelectField, TextField } from '../forms/Fields';
import { NOTHING_CHOSEN, numberOrNullIfEmpty } from '../forms/fieldValues';
import { FormPanel } from '../forms/FormPanel';
import { readPlantTimes } from '../forms/plantTimeFields';
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
export function PlacementForm({ target, onClose, renderConflictLink }: PlacementFormProps) {
  const timeZone = usePlantTimeZone();
  // The server filters the machines (AC-30); R1 is still checked when saving.
  const machines = useMachines({ resourceTypeId: target.resourceType.id, isActive: true });
  const [resourceId, setResourceId] = useState(target.placement === null ? NOTHING_CHOSEN : String(target.placement.resourceId));
  const [start, setStart] = useState(target.placement === null ? '' : utcToPlantLocal(target.placement.plannedStart, timeZone));
  // The UTC time behind the start text; a suggested slot can be in the second hour of the autumn night.
  const [storedStart, setStoredStart] = useState<string | null>(target.placement?.plannedStart ?? null);
  const [timeProblem, setTimeProblem] = useState<ApiProblem | null>(null);

  // Empty fields are sent as null, so the server reports what is missing.
  const saveMutation = useMutation({
    mutationFn: (plannedStart: string | null) => placeLot(SCHEDULE_VERSION, target.lotId, { resourceId: numberOrNullIfEmpty(resourceId), plannedStart }),
    onSuccess: onClose,
  });
  const removeMutation = useMutation({ mutationFn: () => removeLotPlacement(SCHEDULE_VERSION, target.lotId), onSuccess: onClose });
  const problem = timeProblem ?? problemOf(saveMutation.error) ?? problemOf(removeMutation.error);
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
        saveMutation.reset();
        const reading = readPlantTimes({ plannedStart: start }, timeZone, { plannedStart: storedStart });
        setTimeProblem(reading.problem);
        if (reading.problem === null) {
          saveMutation.mutate(reading.utc.plannedStart);
        }
      }}
      extraActions={
        target.placement !== null && (
          <button
            type="button"
            disabled={removeMutation.isPending}
            onClick={() => {
              saveMutation.reset();
              setTimeProblem(null);
              removeMutation.mutate();
            }}
          >
            Remove placement
          </button>
        )
      }
      onCancel={onClose}
    >
      {problem !== null && <ViolationList violations={problem.violations} renderConflictLink={renderConflictLink} />}
      <QueryResult query={machines}>
        {(loadedMachines) => (
          <EarliestSlotFinder
            target={target}
            machines={loadedMachines}
            onUse={(slot) => {
              setResourceId(String(slot.resource.id));
              setStart(utcToPlantLocal(slot.plannedStart, timeZone));
              setStoredStart(slot.plannedStart);
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
        <TextField label="Start" name="plannedStart" type="datetime-local" step={START_GRID_STEP_SECONDS} value={start} onChange={setStart} problem={problem} />
        <p>Planned end: Calculated when saved</p>
      </fieldset>
    </FormPanel>
  );
}
