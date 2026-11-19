import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createResourceType, deleteResourceType, updateResourceType } from '../api/generated/endpoints.gen';
import type { ResourceTypeResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { FormPanel } from '../forms/FormPanel';
import { ProblemSummary } from '../forms/ProblemSummary';

interface MachineTypesPanelProps {
  resourceTypes: readonly ResourceTypeResponse[];
  canEdit: boolean;
}

export function MachineTypesPanel({ resourceTypes, canEdit }: MachineTypesPanelProps) {
  const [typeInForm, setTypeInForm] = useState<ResourceTypeResponse | 'new' | null>(null);
  const deleteMutation = useMutation({
    mutationFn: deleteResourceType,
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
  const [name, setName] = useState(resourceType?.name ?? '');
  const saveMutation = useMutation({
    mutationFn: () => (resourceType === null ? createResourceType({ name }) : updateResourceType(resourceType.id, { name })),
    onSuccess: onClose,
  });
  const problem = problemOf(saveMutation.error);

  return (
    <FormPanel
      accessibleName={resourceType === null ? 'Add machine type' : `Rename machine type ${resourceType.name}`}
      heading={resourceType === null ? 'Add machine type' : `Rename ${resourceType.name}`}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save"
      onSubmit={() => {
        saveMutation.mutate();
      }}
      onCancel={onClose}
    >
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
    </FormPanel>
  );
}
