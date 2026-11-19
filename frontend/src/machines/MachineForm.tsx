import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createResource, updateResource } from '../api/generated/endpoints.gen';
import type { ResourceTypeResponse, ResourceViewResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { CheckboxField, SelectField, TextField } from '../forms/Fields';
import { NOTHING_CHOSEN, numberOrNullIfEmpty } from '../forms/fieldValues';
import { FormPanel } from '../forms/FormPanel';

interface MachineFormProps {
  /** The machine to change, or null to add one. */
  machine: ResourceViewResponse | null;
  resourceTypes: readonly ResourceTypeResponse[];
  onClose: () => void;
}

export function MachineForm({ machine, resourceTypes, onClose }: MachineFormProps) {
  const [name, setName] = useState(machine?.name ?? '');
  const [resourceTypeId, setResourceTypeId] = useState(machine === null ? NOTHING_CHOSEN : String(machine.resourceType.id));
  const [isActive, setIsActive] = useState(machine?.isActive ?? true);

  const saveMutation = useMutation({
    mutationFn: () => {
      const chosenTypeId = numberOrNullIfEmpty(resourceTypeId);
      return machine === null
        ? createResource({ name, resourceTypeId: chosenTypeId })
        : updateResource(machine.id, { name, resourceTypeId: chosenTypeId, isActive });
    },
    onSuccess: onClose,
  });
  const problem = problemOf(saveMutation.error);

  return (
    <FormPanel
      accessibleName={machine === null ? 'Add machine' : `Edit machine ${machine.name}`}
      heading={machine === null ? 'Add machine' : `Edit ${machine.name}`}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save"
      onSubmit={() => {
        saveMutation.mutate();
      }}
      onCancel={onClose}
    >
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <SelectField
        label="Machine type"
        name="resourceTypeId"
        value={resourceTypeId}
        onChange={setResourceTypeId}
        problem={problem}
        options={[{ value: NOTHING_CHOSEN, label: 'Choose a type' }, ...resourceTypes.map((type) => ({ value: String(type.id), label: type.name }))]}
      />
      {machine !== null && <CheckboxField label="Active" name="isActive" checked={isActive} onChange={setIsActive} problem={problem} />}
    </FormPanel>
  );
}
