import type { ReactNode } from 'react';
import { QueryResult } from '../forms/QueryResult';
import { useCalendars } from './calendarQueries';
import { PlantTimeZoneContext } from './plantTimeZone';

/** Shows its children once the calendars are loaded, with the plant time zone for usePlantTimeZone (AC-41, DEC-40). */
export function PlantTimeZoneProvider({ children }: { children: ReactNode }) {
  const calendars = useCalendars();
  return (
    <QueryResult query={calendars}>
      {(loadedCalendars) => {
        // Every calendar is in the plant time zone (AC-41), so any calendar names it.
        const timeZone = loadedCalendars[0]?.timeZone;
        if (timeZone === undefined) {
          throw new Error('The API returned no calendar, so the plant time zone is unknown.');
        }
        return <PlantTimeZoneContext value={timeZone}>{children}</PlantTimeZoneContext>;
      }}
    </QueryResult>
  );
}
