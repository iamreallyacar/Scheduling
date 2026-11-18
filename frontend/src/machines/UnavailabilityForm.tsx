import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createUnavailability, updateUnavailability } from '../api/generated/endpoints.gen';
import type { UnavailabilityRequest, UnavailabilityResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { plantLocalToUtc, utcToPlantLocal } from '../calendar/plantClock';
import { SelectField, TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { UNAVAILABILITY_KIND_LABELS, UNAVAILABILITY_KINDS } from './machineValues';

interface UnavailabilityFormProps {
  resourceId: number;
  /** The unavailability to change, or null to record one. */
  unavailability: UnavailabilityResponse | null;
  timeZone: string;
  onClose: () => void;
}

const NO_KIND_CHOSEN = '';

// F-MNT-1. Times are typed on the plant's wall clock and sent in UTC (A38); the server checks them.
export function UnavailabilityForm({ resourceId, unavailability, timeZone, onClose }: UnavailabilityFormProps) {
  const [kind, setKind] = useState<string>(unavailability?.kind ?? NO_KIND_CHOSEN);
  const [start, setStart] = useState(unavailability === null ? '' : utcToPlantLocal(unavailability.start, timeZone));
  const [end, setEnd] = useState(unavailability?.end == null ? '' : utcToPlantLocal(unavailability.end, timeZone));
  const [reason, setReason] = useState(unavailability?.reason ?? '');

  const saveMutation = useMutation({
    mutationFn: () => {
      // Empty fields are sent as null, so the server reports what is missing.
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

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label={title}>
      <h3>{title}</h3>
      <ProblemSummary problem={problem} />
      <SelectField
        label="Kind"
        name="kind"
        value={kind}
        onChange={setKind}
        problem={problem}
        options={[
          { value: NO_KIND_CHOSEN, label: 'Choose a kind' },
          ...UNAVAILABILITY_KINDS.map((value) => ({ value, label: UNAVAILABILITY_KIND_LABELS[value] })),
        ]}
      />
      <TextField label="Start" name="start" type="datetime-local" value={start} onChange={setStart} problem={problem} />
      <TextField label="End" name="end" type="datetime-local" value={end} onChange={setEnd} problem={problem} />
      <p>Leave the end empty for a breakdown with no known end.</p>
      <TextField label="Reason" name="reason" value={reason} onChange={setReason} problem={problem} />
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
