import { useCalendars } from './calendarQueries';

// Times are shown on the plant's wall clock, not the browser's (ARCHITECTURE.md section "Time rules").
// Every calendar is in the plant time zone (AC-41), so any calendar names it.
export function PlantTime({ value }: { value: string }) {
  const calendars = useCalendars();
  const timeZone = calendars.data?.[0]?.timeZone;
  if (timeZone === undefined) {
    return null;
  }
  const formatter = new Intl.DateTimeFormat('en-GB', { timeZone, dateStyle: 'medium', timeStyle: 'short' });
  return <time dateTime={value}>{formatter.format(new Date(value))}</time>;
}
