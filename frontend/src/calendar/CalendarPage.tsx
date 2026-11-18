import { useSignedInUser } from '../auth/signedInUser';
import { QueryResult } from '../forms/QueryResult';
import { useCalendars, useHolidays } from './calendarQueries';
import { HolidayForm } from './HolidayForm';
import { HolidayList } from './HolidayList';
import { ShiftEditor } from './ShiftEditor';
import { ShiftTable } from './ShiftTable';

// F-MD-5: the plant calendar and the holiday list. Everyone reads; an Admin edits.
export function CalendarPage() {
  const { canAdminister } = useSignedInUser();
  const calendars = useCalendars();
  const holidays = useHolidays();

  return (
    <section aria-labelledby="calendar-heading">
      <h1 id="calendar-heading">Plant calendar</h1>
      <QueryResult query={calendars}>
        {(loadedCalendars) =>
          loadedCalendars.map((calendar) => (
            <section key={calendar.id} aria-label={calendar.name}>
              <h2>{calendar.name}</h2>
              <p className="page-message">Time zone: {calendar.timeZone}</p>
              {canAdminister ? <ShiftEditor calendar={calendar} /> : <ShiftTable calendar={calendar} />}
            </section>
          ))
        }
      </QueryResult>
      <h2>Holidays</h2>
      <p className="page-message">No calendar has working time on a holiday.</p>
      <QueryResult query={holidays}>{(loadedHolidays) => <HolidayList holidays={loadedHolidays} canDelete={canAdminister} />}</QueryResult>
      {canAdminister && <HolidayForm />}
    </section>
  );
}
