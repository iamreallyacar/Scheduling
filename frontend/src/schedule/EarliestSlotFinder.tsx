import { useState } from 'react';
import type { EarliestSlotResponse, ResourceViewResponse } from '../api/generated';
import { problemOf, type ApiProblem } from '../api/problem';
import { formatPlantTime } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { START_GRID_STEP_SECONDS, SelectField, TextField } from '../forms/Fields';
import { readPlantTimes } from '../forms/plantTimeFields';
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
  // The search as it was when the planner pressed the button; null before the first press.
  const [search, setSearch] = useState<EarliestSlotSearch | null>(null);
  const [timeProblem, setTimeProblem] = useState<ApiProblem | null>(null);
  // A suggestion answers the fields as they were pressed, so changing either hides it until the next press.
  const changeSearchField = (change: () => void) => {
    change();
    setSearch(null);
    setTimeProblem(null);
  };
  const earliestSlot = useEarliestSlot(target.lotId, search);
  const problem = timeProblem ?? (earliestSlot.isError ? problemOf(earliestSlot.error) : null);
  const findSlot = () => {
    const reading = readPlantTimes({ notBefore }, timeZone);
    setTimeProblem(reading.problem);
    if (reading.problem !== null) {
      return;
    }
    const typedSearch: EarliestSlotSearch = {};
    if (searchResourceId !== ANY_MACHINE) {
      typedSearch.resourceId = Number(searchResourceId);
    }
    // Without notBefore the server searches from its own now (AC-125).
    if (reading.utc.notBefore !== null) {
      typedSearch.notBefore = reading.utc.notBefore;
    }
    // The same search is asked again, because now has moved on.
    if (search !== null && JSON.stringify(typedSearch) === JSON.stringify(search)) {
      void earliestSlot.refetch();
    } else {
      setSearch(typedSearch);
    }
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
        onChange={(value) => {
          changeSearchField(() => {
            setSearchResourceId(value);
          });
        }}
        problem={problem}
        options={[
          { value: ANY_MACHINE, label: `Any ${target.resourceType.name}` },
          ...machines.map((machine) => ({ value: String(machine.id), label: machine.name })),
        ]}
      />
      <TextField
        label="Not before"
        name="notBefore"
        type="datetime-local"
        step={START_GRID_STEP_SECONDS}
        value={notBefore}
        onChange={(value) => {
          changeSearchField(() => {
            setNotBefore(value);
          });
        }}
        problem={problem}
      />
      <p>Empty means now.</p>
      <div className="form-actions">
        <button type="button" disabled={earliestSlot.isFetching} onClick={findSlot}>
          Find earliest slot
        </button>
      </div>
      <ProblemSummary problem={problem} />
      {problem !== null && <ViolationList violations={problem.violations} />}
      {search !== null && earliestSlot.isSuccess && (
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
