import type { CalendarResponse } from '../api/generated';

/** The shifts of a calendar for reading only, in the order the server returns them. */
export function ShiftTable({ calendar }: { calendar: CalendarResponse }) {
  if (calendar.shifts.length === 0) {
    return <p className="page-message">This calendar has no shifts, so it has no working time.</p>;
  }
  return (
    <table aria-label="Shifts">
      <thead>
        <tr>
          <th scope="col">Weekday</th>
          <th scope="col">Start</th>
          <th scope="col">End</th>
        </tr>
      </thead>
      <tbody>
        {calendar.shifts.map((shift) => (
          <tr key={`${shift.weekday}-${shift.start}`}>
            <th scope="row">{shift.weekday}</th>
            <td>{shift.start}</td>
            <td>{shift.end}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
