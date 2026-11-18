import { useQuery } from '@tanstack/react-query';
import { listCalendars, listHolidays } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';

export function useCalendars() {
  return useQuery({ queryKey: queryKeys.calendars, queryFn: listCalendars });
}

export function useHolidays() {
  return useQuery({ queryKey: queryKeys.holidays, queryFn: listHolidays });
}

// Every calendar is in the plant time zone (AC-41), so any calendar names it. Undefined until
// the calendars are loaded.
export function usePlantTimeZone(): string | undefined {
  return useCalendars().data?.[0]?.timeZone;
}
