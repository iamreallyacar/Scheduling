import { useQuery } from '@tanstack/react-query';
import { listCalendars, listHolidays } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';

export function useCalendars() {
  return useQuery({ queryKey: queryKeys.calendars, queryFn: listCalendars });
}

export function useHolidays() {
  return useQuery({ queryKey: queryKeys.holidays, queryFn: listHolidays });
}
