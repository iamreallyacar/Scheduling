import { formatPlantTime } from './plantClock';
import { usePlantTimeZone } from './plantTimeZone';

// Times are shown on the plant's wall clock, not the browser's (ARCHITECTURE.md section "Time rules").
export function PlantTime({ value }: { value: string }) {
  const timeZone = usePlantTimeZone();
  return <time dateTime={value}>{formatPlantTime(value, timeZone)}</time>;
}
