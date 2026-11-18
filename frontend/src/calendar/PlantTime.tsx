import { usePlantTimeZone } from './calendarQueries';
import { formatPlantTime } from './plantClock';

// Times are shown on the plant's wall clock, not the browser's (ARCHITECTURE.md section "Time rules").
export function PlantTime({ value }: { value: string }) {
  const timeZone = usePlantTimeZone();
  if (timeZone === undefined) {
    return null;
  }
  return <time dateTime={value}>{formatPlantTime(value, timeZone)}</time>;
}
