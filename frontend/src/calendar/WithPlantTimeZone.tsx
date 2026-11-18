import type { ReactNode } from 'react';
import { QueryResult } from '../forms/QueryResult';
import { useCalendars } from './calendarQueries';

/** Renders its children with the plant time zone once the calendars are loaded (AC-41, DEC-40). */
export function WithPlantTimeZone({ children }: { children: (timeZone: string) => ReactNode }) {
  const calendars = useCalendars();
  return (
    <QueryResult query={calendars}>
      {(loadedCalendars) => {
        const timeZone = loadedCalendars[0]?.timeZone;
        if (timeZone === undefined) {
          throw new Error('The API returned no calendar, so the plant time zone is unknown.');
        }
        return children(timeZone);
      }}
    </QueryResult>
  );
}
