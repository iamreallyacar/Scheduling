import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createUnavailability, updateUnavailability } from '../api/generated/endpoints.gen';
import type { UnavailabilityRequest, UnavailabilityResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { plantLocalToUtc, utcToPlantLocal } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { SelectField, TextField } from '../forms/Fields';
import { NOTHING_CHOSEN } from '../forms/fieldValues';
import { FormPanel } from '../forms/FormPanel';
import { UNAVAILABILITY_KIND_LABELS, UNAVAILABILITY_KINDS } from './machineValues';

interface UnavailabilityFormProps {
  resourceId: number;
  /** The unavailability to change, or null to record one. */
  unavailability: UnavailabilityResponse | null;
  onClose: () => void;
}

// F-MNT-1. Times are typed on the plant's wall clock and sent in UTC (A38); the server checks them.
export function UnavailabilityForm({ resourceId, unavailability, onClose }: UnavailabilityFormProps) {
  const timeZone = usePlantTimeZone();
  const [kind, setKind] = useState<string>(unavailability?.kind ?? NOTHING_CHOSEN);
  const [start, setStart] = useState(unavailability === null ? '' : utcToPlantLocal(unavailability.start, timeZone));
  const [end, setEnd] = useState(unavailability?.end == null ? '' : utcToPlantLocal(unavailability.end, timeZone));
  const [reason, setReason] = useState(unavailability?.reason ?? '');

  const saveMutation = useMutation({
    mutationFn: () => {
      const request: UnavailabilityRequest = {
        kind: UNAVAILABILITY_KINDS.find((value) => value === kind) ?? null,
        start: start === '' ? null : plantLocalToUtc(start, timeZone),
        end: end === '' ? null : plantLocalToUtc(end, timeZone),
        reason,
      };
      return unavailability === null
        ? createUnavailability(resourceId, request)
        : updateUnavailability(resourceId, unavailability.id, request);
    },
    onSuccess: onClose,
  });
  const problem = problemOf(saveMutation.error);
  const title = unavailability === null ? 'Record unavailability' : 'Edit unavailability';

  return (
    <FormPanel
      accessibleName={title}
      heading={title}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save"
      onSubmit={() => {
        saveMutation.mutate();
      }}
      onCancel={onClose}
    >
      <SelectField
        label="Kind"
        name="kind"
        value={kind}
        onChange={setKind}
        problem={problem}
        options={[
          { value: NOTHING_CHOSEN, label: 'Choose a kind' },
          ...UNAVAILABILITY_KINDS.map((value) => ({ value, label: UNAVAILABILITY_KIND_LABELS[value] })),
        ]}
      />
      <TextField label="Start" name="start" type="datetime-local" value={start} onChange={setStart} problem={problem} />
      <TextField label="End" name="end" type="datetime-local" value={end} onChange={setEnd} problem={problem} />
      <p>Leave the end empty for a breakdown with no known end.</p>
      <TextField label="Reason" name="reason" value={reason} onChange={setReason} problem={problem} />
    </FormPanel>
  );
}
