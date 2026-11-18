import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { deleteResource } from '../api/generated/endpoints.gen';
import type { ResourceTypeResponse, ResourceViewResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { useSignedInUser } from '../auth/signedInUser';
import { ProblemSummary } from '../forms/ProblemSummary';
import { QueryResult } from '../forms/QueryResult';
import { MachineForm } from './MachineForm';
import { useMachines, useResourceTypes, type MachineFilter } from './machineQueries';
import { RESOURCE_STATUS_LABELS } from './machineValues';
import { MachineTypesPanel } from './MachineTypesPanel';

const ANY = '';

// F-MD-1 and F-MNT-2: the machine register, with its machine types and the status the server
// derived. Everyone reads; an Admin edits. Each name opens the machine page.
export function MachineRegisterPage() {
  const { canAdminister } = useSignedInUser();
  const [filter, setFilter] = useState<MachineFilter>({ resourceTypeId: null, isActive: null });
  const [machineInForm, setMachineInForm] = useState<ResourceViewResponse | 'new' | null>(null);
  const resourceTypes = useResourceTypes();
  const machines = useMachines(filter);
  const deleteMutation = useMutation({
    mutationFn: deleteResource,
  });

  return (
    <QueryResult query={resourceTypes}>
      {(types) => (
        <>
          <section aria-labelledby="machines-heading">
            <h1 id="machines-heading">Machines</h1>
            <MachineFilterBar filter={filter} onChange={setFilter} resourceTypes={types} />
            <ProblemSummary problem={problemOf(deleteMutation.error)} />
            <QueryResult query={machines}>
              {(listedMachines) => (
                <MachineTable
                  machines={listedMachines}
                  canEdit={canAdminister}
                  onEdit={setMachineInForm}
                  onDelete={(machine) => {
                    deleteMutation.mutate(machine.id);
                  }}
                />
              )}
            </QueryResult>
            {canAdminister && machineInForm === null && (
              <button
                type="button"
                onClick={() => {
                  setMachineInForm('new');
                }}
              >
                Add machine
              </button>
            )}
            {machineInForm !== null && (
              <MachineForm
                // A new key gives the form fresh state when another machine is chosen.
                key={machineInForm === 'new' ? 'new' : machineInForm.id}
                machine={machineInForm === 'new' ? null : machineInForm}
                resourceTypes={types}
                onClose={() => {
                  setMachineInForm(null);
                }}
              />
            )}
          </section>
          <MachineTypesPanel resourceTypes={types} canEdit={canAdminister} />
        </>
      )}
    </QueryResult>
  );
}

interface MachineFilterBarProps {
  filter: MachineFilter;
  onChange: (filter: MachineFilter) => void;
  resourceTypes: readonly ResourceTypeResponse[];
}

function MachineFilterBar({ filter, onChange, resourceTypes }: MachineFilterBarProps) {
  return (
    <div className="filter-bar">
      <label>
        Type{' '}
        <select
          value={filter.resourceTypeId === null ? ANY : String(filter.resourceTypeId)}
          onChange={(event) => {
            onChange({ ...filter, resourceTypeId: event.target.value === ANY ? null : Number(event.target.value) });
          }}
        >
          <option value={ANY}>All types</option>
          {resourceTypes.map((type) => (
            <option key={type.id} value={type.id}>
              {type.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Show{' '}
        <select
          value={filter.isActive === null ? ANY : String(filter.isActive)}
          onChange={(event) => {
            onChange({ ...filter, isActive: event.target.value === ANY ? null : event.target.value === 'true' });
          }}
        >
          <option value={ANY}>Active and inactive</option>
          <option value="true">Active only</option>
          <option value="false">Inactive only</option>
        </select>
      </label>
    </div>
  );
}

interface MachineTableProps {
  machines: readonly ResourceViewResponse[];
  canEdit: boolean;
  onEdit: (machine: ResourceViewResponse) => void;
  onDelete: (machine: ResourceViewResponse) => void;
}

function MachineTable({ machines, canEdit, onEdit, onDelete }: MachineTableProps) {
  if (machines.length === 0) {
    return <p className="page-message">No machines match.</p>;
  }
  return (
    <table>
      <thead>
        <tr>
          <th scope="col">Name</th>
          <th scope="col">Type</th>
          <th scope="col">Calendar</th>
          <th scope="col">Active</th>
          <th scope="col">Status</th>
          {canEdit && <th scope="col">Actions</th>}
        </tr>
      </thead>
      <tbody>
        {machines.map((machine) => (
          <tr key={machine.id}>
            <th scope="row">
              <Link to={`/machines/${String(machine.id)}`}>{machine.name}</Link>
            </th>
            <td>{machine.resourceType.name}</td>
            <td>{machine.calendar.name}</td>
            <td>{machine.isActive ? 'Yes' : 'No'}</td>
            <td>{RESOURCE_STATUS_LABELS[machine.status]}</td>
            {canEdit && (
              <td className="row-actions">
                <button
                  type="button"
                  aria-label={`Edit ${machine.name}`}
                  onClick={() => {
                    onEdit(machine);
                  }}
                >
                  Edit
                </button>
                <button
                  type="button"
                  aria-label={`Delete ${machine.name}`}
                  onClick={() => {
                    onDelete(machine);
                  }}
                >
                  Delete
                </button>
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
