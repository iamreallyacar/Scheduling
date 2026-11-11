import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createResource, updateResource } from '../api/endpoints';
import type { ResourceResponse, ResourceTypeResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { CheckboxField, SelectField, TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { reloadMachineData } from './machineQueries';

interface MachineFormProps {
  /** The machine to change, or null to add one. */
  machine: ResourceResponse | null;
  resourceTypes: readonly ResourceTypeResponse[];
  onClose: () => void;
}

const NO_TYPE_CHOSEN = '';

export function MachineForm({ machine, resourceTypes, onClose }: MachineFormProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(machine?.name ?? '');
  const [resourceTypeId, setResourceTypeId] = useState(machine === null ? NO_TYPE_CHOSEN : String(machine.resourceType.id));
  const [isActive, setIsActive] = useState(machine?.isActive ?? true);

  const saveMutation = useMutation({
    mutationFn: () => {
      // An empty choice is sent as null, so the server reports the missing field.
      const chosenTypeId = resourceTypeId === NO_TYPE_CHOSEN ? null : Number(resourceTypeId);
      return machine === null
        ? createResource({ name, resourceTypeId: chosenTypeId })
        : updateResource(machine.id, { name, resourceTypeId: chosenTypeId, isActive });
    },
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
    <form className="panel" onSubmit={submit} noValidate aria-label={machine === null ? 'Add machine' : `Edit machine ${machine.name}`}>
      <h3>{machine === null ? 'Add machine' : `Edit ${machine.name}`}</h3>
      <ProblemSummary problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <SelectField
        label="Machine type"
        name="resourceTypeId"
        value={resourceTypeId}
        onChange={setResourceTypeId}
        problem={problem}
        options={[{ value: NO_TYPE_CHOSEN, label: 'Choose a type' }, ...resourceTypes.map((type) => ({ value: String(type.id), label: type.name }))]}
      />
      {machine !== null && <CheckboxField label="Active" name="isActive" checked={isActive} onChange={setIsActive} problem={problem} />}
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
