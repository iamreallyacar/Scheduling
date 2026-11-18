import { useMutation } from '@tanstack/react-query';
import { deleteHoliday } from '../api/generated/endpoints.gen';
import type { HolidayResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { ProblemSummary } from '../forms/ProblemSummary';

interface HolidayListProps {
  holidays: readonly HolidayResponse[];
  canDelete: boolean;
}

/** The holidays in the order the server returns them (by date). */
export function HolidayList({ holidays, canDelete }: HolidayListProps) {
  const deleteMutation = useMutation({
    mutationFn: deleteHoliday,
  });

  return (
    <>
      <ProblemSummary problem={problemOf(deleteMutation.error)} />
      {holidays.length === 0 ? (
        <p className="page-message">No holidays yet.</p>
      ) : (
        <table aria-label="Holidays">
          <thead>
            <tr>
              <th scope="col">Date</th>
              <th scope="col">Name</th>
              {canDelete && <th scope="col">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {holidays.map((holiday) => (
              <tr key={holiday.id}>
                <th scope="row">{holiday.date}</th>
                <td>{holiday.name}</td>
                {canDelete && (
                  <td className="row-actions">
                    <button
                      type="button"
                      aria-label={`Delete ${holiday.name}`}
                      disabled={deleteMutation.isPending}
                      onClick={() => {
                        deleteMutation.mutate(holiday.id);
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
      )}
    </>
  );
}
