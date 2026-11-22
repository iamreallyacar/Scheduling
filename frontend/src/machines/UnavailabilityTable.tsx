import { useMutation } from '@tanstack/react-query';
import { deleteUnavailability } from '../api/generated/endpoints.gen';
import type { UnavailabilityResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { formatPlantTime } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { ProblemSummary } from '../forms/ProblemSummary';
import { UNAVAILABILITY_KIND_LABELS } from './machineValues';

interface UnavailabilityTableProps {
  resourceId: number;
  unavailabilities: readonly UnavailabilityResponse[];
  canEdit: boolean;
  onEdit: (unavailability: UnavailabilityResponse) => void;
}

/** The unavailabilities in the order the server returns them (latest start first). */
export function UnavailabilityTable({ resourceId, unavailabilities, canEdit, onEdit }: UnavailabilityTableProps) {
  const timeZone = usePlantTimeZone();
  const deleteMutation = useMutation({
    mutationFn: (unavailabilityId: number) => deleteUnavailability(resourceId, unavailabilityId),
  });

  if (unavailabilities.length === 0) {
    return <p className="page-message">No unavailability recorded.</p>;
  }
  return (
    <>
      <ProblemSummary problem={problemOf(deleteMutation.error)} />
      <table aria-label="Unavailabilities">
        <thead>
          <tr>
            <th scope="col">Kind</th>
            <th scope="col">Start</th>
            <th scope="col">End</th>
            <th scope="col">Reason</th>
            {canEdit && <th scope="col">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {unavailabilities.map((unavailability) => {
            const kind = UNAVAILABILITY_KIND_LABELS[unavailability.kind];
            const start = formatPlantTime(unavailability.start, timeZone);
            const name = `${kind.toLowerCase()} from ${start}`;
            return (
              <tr key={unavailability.id}>
                <th scope="row">{kind}</th>
                <td>
                  <time dateTime={unavailability.start}>{start}</time>
                </td>
                <td>{unavailability.end === null ? 'no end' : <time dateTime={unavailability.end}>{formatPlantTime(unavailability.end, timeZone)}</time>}</td>
                <td>{unavailability.reason}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button
                      type="button"
                      aria-label={`Edit ${name}`}
                      onClick={() => {
                        onEdit(unavailability);
                      }}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      aria-label={`Delete ${name}`}
                      disabled={deleteMutation.isPending}
                      onClick={() => {
                        deleteMutation.mutate(unavailability.id);
                      }}
                    >
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </>
  );
}
