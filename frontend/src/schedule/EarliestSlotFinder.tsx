import { useState } from 'react';
import type { EarliestSlotResponse, ResourceViewResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { formatPlantTime, plantLocalToUtc } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { SelectField, TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import type { PlacementTarget } from './placementTarget';
import { useEarliestSlot, type EarliestSlotSearch } from './scheduleQueries';
import { ViolationList } from './ViolationList';

// Searching every active machine of the lot's type is asked for by leaving the machine out.
const ANY_MACHINE = '';

interface EarliestSlotFinderProps {
  target: PlacementTarget;
  /** The machines the Placement part offers: the active ones of the lot's type. */
  machines: readonly ResourceViewResponse[];
  onUse: (slot: EarliestSlotResponse) => void;
}

/** F-SCH-11 (wireframes.md section 3): the server searches and checks every rule; the part shows its answer and saves nothing (AC-196). */
export function EarliestSlotFinder({ target, machines, onUse }: EarliestSlotFinderProps) {
  const timeZone = usePlantTimeZone();
  const [searchResourceId, setSearchResourceId] = useState(ANY_MACHINE);
  const [notBefore, setNotBefore] = useState('');
  const search: EarliestSlotSearch = {};
  if (searchResourceId !== ANY_MACHINE) {
    search.resourceId = Number(searchResourceId);
  }
  // Without notBefore the server searches from its own now (AC-125).
  if (notBefore !== '') {
    search.notBefore = plantLocalToUtc(notBefore, timeZone);
  }
  const earliestSlot = useEarliestSlot(target.lotId, search);
  const problem = earliestSlot.isError ? problemOf(earliestSlot.error) : null;
  const findSlot = () => {
    void earliestSlot.refetch();
  };

  return (
    // The part sits inside the placement form, so Enter in its time field would save the
    // placement; it searches instead.
    <fieldset
      onKeyDown={(event) => {
        if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
          event.preventDefault();
          findSlot();
        }
      }}
    >
      <legend>Find earliest slot</legend>
      {/* The names are the API's parameters, so the search's field errors show here; the
          Placement part reads only the problem of a save. */}
      <SelectField
        label="Search on"
        name="resourceId"
        value={searchResourceId}
        onChange={setSearchResourceId}
        problem={problem}
        options={[
          { value: ANY_MACHINE, label: `Any ${target.resourceType.name}` },
          ...machines.map((machine) => ({ value: String(machine.id), label: machine.name })),
        ]}
      />
      <TextField label="Not before" name="notBefore" type="datetime-local" value={notBefore} onChange={setNotBefore} problem={problem} />
      <p>Empty means now.</p>
      <div className="form-actions">
        <button type="button" disabled={earliestSlot.isFetching} onClick={findSlot}>
          Find earliest slot
        </button>
      </div>
      <ProblemSummary problem={problem} />
      {problem !== null && <ViolationList violations={problem.violations} />}
      {earliestSlot.isSuccess && (
        <p>
          {`Suggested: ${earliestSlot.data.resource.name}, ${formatPlantTime(earliestSlot.data.plannedStart, timeZone)} - ${formatPlantTime(earliestSlot.data.plannedEnd, timeZone)}`}{' '}
          <button
            type="button"
            onClick={() => {
              onUse(earliestSlot.data);
            }}
          >
            Use suggestion
          </button>
        </p>
      )}
    </fieldset>
  );
}
