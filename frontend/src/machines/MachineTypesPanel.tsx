import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createResourceType, deleteResourceType, updateResourceType } from '../api/endpoints';
import type { ResourceTypeResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { reloadMachineData } from './machineQueries';

interface MachineTypesPanelProps {
  resourceTypes: readonly ResourceTypeResponse[];
  canEdit: boolean;
}

export function MachineTypesPanel({ resourceTypes, canEdit }: MachineTypesPanelProps) {
  const queryClient = useQueryClient();
  const [typeInForm, setTypeInForm] = useState<ResourceTypeResponse | 'new' | null>(null);
  const deleteMutation = useMutation({
    mutationFn: deleteResourceType,
    onSuccess: () => reloadMachineData(queryClient),
  });

  return (
    <section aria-labelledby="machine-types-heading">
      <h2 id="machine-types-heading">Machine types</h2>
      <ProblemSummary problem={problemOf(deleteMutation.error)} />
      {resourceTypes.length === 0 ? (
        <p className="page-message">No machine types yet.</p>
      ) : (
        <ul className="plain-list">
          {resourceTypes.map((type) => (
            <li key={type.id}>
              <span>{type.name}</span>
              {canEdit && (
                <span className="row-actions">
                  <button
                    type="button"
                    aria-label={`Rename ${type.name}`}
                    onClick={() => {
                      setTypeInForm(type);
                    }}
                  >
                    Rename
                  </button>
                  <button
                    type="button"
                    aria-label={`Delete ${type.name}`}
                    disabled={deleteMutation.isPending}
                    onClick={() => {
                      deleteMutation.mutate(type.id);
                    }}
                  >
                    Delete
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
      {canEdit && typeInForm === null && (
        <button
          type="button"
          onClick={() => {
            setTypeInForm('new');
          }}
        >
          Add machine type
        </button>
      )}
      {typeInForm !== null && (
        <MachineTypeForm
          // A new key gives the form fresh state when another type is chosen.
          key={typeInForm === 'new' ? 'new' : typeInForm.id}
          resourceType={typeInForm === 'new' ? null : typeInForm}
          onClose={() => {
            setTypeInForm(null);
          }}
        />
      )}
    </section>
  );
}

function MachineTypeForm({ resourceType, onClose }: { resourceType: ResourceTypeResponse | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(resourceType?.name ?? '');
  const saveMutation = useMutation({
    mutationFn: () => (resourceType === null ? createResourceType({ name }) : updateResourceType(resourceType.id, { name })),
    onSuccess: async () => {
      await reloadMachineData(queryClient);
      onClose();
    },
  });
  const problem = problemOf(saveMutation.error);

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label={resourceType === null ? 'Add machine type' : `Rename machine type ${resourceType.name}`}>
      <h3>{resourceType === null ? 'Add machine type' : `Rename ${resourceType.name}`}</h3>
      <ProblemSummary problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save
        </button>
        <button type="button" onClick={onClose}>
          Cancel
        </button>
      </div>
    </form>
  );
}
