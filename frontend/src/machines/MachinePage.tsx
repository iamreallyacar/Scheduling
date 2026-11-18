import { useState } from 'react';
import { Link, useParams } from 'react-router';
import type { ResourceViewResponse, UnavailabilityResponse } from '../api/generated';
import { useSignedInUser } from '../auth/signedInUser';
import { useCalendars } from '../calendar/calendarQueries';
import { QueryResult } from '../forms/QueryResult';
import { NotFoundPage } from '../shell/NotFoundPage';
import { useMachine, useUnavailabilities } from './machineQueries';
import { RESOURCE_STATUS_LABELS } from './machineValues';
import { UnavailabilityForm } from './UnavailabilityForm';
import { UnavailabilityTable } from './UnavailabilityTable';

// F-MNT-1 and F-MNT-2: one machine with the status the server derived and its unavailabilities
// (wireframes.md section 5). Everyone reads; a Planner records unavailabilities.
export function MachinePage() {
  const resourceId = Number(useParams()['machineId']);
  return Number.isInteger(resourceId) ? <MachineView resourceId={resourceId} /> : <NotFoundPage />;
}

function MachineView({ resourceId }: { resourceId: number }) {
  const machine = useMachine(resourceId);
  const calendars = useCalendars();
  return (
    <section aria-labelledby="machine-heading">
      <p>
        <Link to="/machines">Back to machines</Link>
      </p>
      <QueryResult query={calendars}>
        {(loadedCalendars) => (
          <QueryResult query={machine}>
            {(loadedMachine) => <LoadedMachine machine={loadedMachine} timeZone={loadedCalendars[0]?.timeZone} />}
          </QueryResult>
        )}
      </QueryResult>
    </section>
  );
}

function LoadedMachine({ machine, timeZone }: { machine: ResourceViewResponse; timeZone: string | undefined }) {
  const { canPlan } = useSignedInUser();
  const unavailabilities = useUnavailabilities(machine.id);
  const [unavailabilityInForm, setUnavailabilityInForm] = useState<UnavailabilityResponse | 'new' | null>(null);
  if (timeZone === undefined) {
    throw new Error('The API returned no calendar, so the plant time zone is unknown.');
  }

  return (
    <>
      <h1 id="machine-heading">{machine.name}</h1>
      <p>Times in {timeZone}</p>
      <dl className="field-list">
        <dt>Type</dt>
        <dd>{machine.resourceType.name}</dd>
        <dt>Calendar</dt>
        <dd>{machine.calendar.name}</dd>
        <dt>Active</dt>
        <dd>{machine.isActive ? 'Yes' : 'No'}</dd>
        <dt>Status</dt>
        <dd>{RESOURCE_STATUS_LABELS[machine.status]}</dd>
      </dl>
      <h2>Unavailability</h2>
      <QueryResult query={unavailabilities}>
        {(loadedUnavailabilities) => (
          <UnavailabilityTable
            resourceId={machine.id}
            unavailabilities={loadedUnavailabilities}
            timeZone={timeZone}
            canEdit={canPlan}
            onEdit={setUnavailabilityInForm}
          />
        )}
      </QueryResult>
      {canPlan && unavailabilityInForm === null && (
        <button
          type="button"
          onClick={() => {
            setUnavailabilityInForm('new');
          }}
        >
          Record unavailability
        </button>
      )}
      {unavailabilityInForm !== null && (
        <UnavailabilityForm
          // A new key gives the form fresh state when another unavailability is chosen.
          key={unavailabilityInForm === 'new' ? 'new' : unavailabilityInForm.id}
          resourceId={machine.id}
          unavailability={unavailabilityInForm === 'new' ? null : unavailabilityInForm}
          timeZone={timeZone}
          onClose={() => {
            setUnavailabilityInForm(null);
          }}
        />
      )}
    </>
  );
}
