import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { recordLotProgress, removeLotProgress } from '../api/generated/endpoints.gen';
import { problemOf, type ApiProblem } from '../api/problem';
import { utcToPlantLocal } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { TextField } from '../forms/Fields';
import { FormPanel } from '../forms/FormPanel';
import { readPlantTimes } from '../forms/plantTimeFields';

/** The lot whose progress the form records, with the progress it has. */
export interface ProgressTarget {
  lotId: number;
  /** As `lotTitle` names the lot. */
  title: string;
  actualStart: string | null;
  actualEnd: string | null;
}

interface LotProgressFormProps {
  target: ProgressTarget;
  onClose: () => void;
}

// F-PRG-1. The progress is saved as a whole (A47); times are typed on the plant's wall clock and
// sent in UTC (A38), and the server checks them.
export function LotProgressForm({ target, onClose }: LotProgressFormProps) {
  const timeZone = usePlantTimeZone();
  const [start, setStart] = useState(target.actualStart === null ? '' : utcToPlantLocal(target.actualStart, timeZone));
  const [end, setEnd] = useState(target.actualEnd === null ? '' : utcToPlantLocal(target.actualEnd, timeZone));

  const [timeProblem, setTimeProblem] = useState<ApiProblem | null>(null);

  // An empty start is sent as null, so the server reports it as missing.
  const saveMutation = useMutation({
    mutationFn: (times: { actualStart: string | null; actualEnd: string | null }) => recordLotProgress(target.lotId, times),
    onSuccess: onClose,
  });
  const clearMutation = useMutation({ mutationFn: () => removeLotProgress(target.lotId), onSuccess: onClose });
  const problem = timeProblem ?? problemOf(saveMutation.error) ?? problemOf(clearMutation.error);
  const title = `Progress - ${target.title}`;

  return (
    <FormPanel
      accessibleName={title}
      heading={title}
      description={<p>Times in {timeZone}.</p>}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save progress"
      onSubmit={() => {
        clearMutation.reset();
        saveMutation.reset();
        const reading = readPlantTimes({ actualStart: start, actualEnd: end }, timeZone, { actualStart: target.actualStart, actualEnd: target.actualEnd });
        setTimeProblem(reading.problem);
        if (reading.problem === null) {
          saveMutation.mutate(reading.utc);
        }
      }}
      extraActions={
        target.actualStart !== null && (
          <button
            type="button"
            disabled={clearMutation.isPending}
            onClick={() => {
              saveMutation.reset();
              setTimeProblem(null);
              clearMutation.mutate();
            }}
          >
            Clear progress
          </button>
        )
      }
      onCancel={onClose}
    >
      <TextField label="Actual start" name="actualStart" type="datetime-local" value={start} onChange={setStart} problem={problem} />
      <TextField label="Actual end" name="actualEnd" type="datetime-local" value={end} onChange={setEnd} problem={problem} />
      <p>Leave the end empty while the lot runs.</p>
    </FormPanel>
  );
}
