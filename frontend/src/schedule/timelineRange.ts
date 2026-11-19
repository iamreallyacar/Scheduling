import { DAY_MS, DISPLAY_LOCALE, utcToPlantLocal } from '../calendar/plantClock';

// The days the timeline shows (A44). Dates are plant-local and written YYYY-MM-DD, as the API
// takes them; date arithmetic runs on UTC dates so the browser zone and DST never shift a day.

export type TimelineZoom = 'day' | 'two-weeks';

export const DAYS_SHOWN: Readonly<Record<TimelineZoom, number>> = { day: 1, 'two-weeks': 14 };

export function plantToday(timeZone: string, now: Date): string {
  return utcToPlantLocal(now.toISOString(), timeZone).slice(0, 10);
}

export function addDays(date: string, days: number): string {
  return new Date(dateToUtcMs(date) + days * DAY_MS).toISOString().slice(0, 10);
}

export function mondayOf(date: string): string {
  // getUTCDay counts from Sunday.
  const daysSinceMonday = (new Date(dateToUtcMs(date)).getUTCDay() + 6) % 7;
  return addDays(date, -daysSinceMonday);
}

/** The first day [Today] shows: today itself, or the Monday of its week for two weeks. */
export function firstDayHolding(today: string, zoom: TimelineZoom): string {
  return zoom === 'day' ? today : mondayOf(today);
}

export function lastShownDay(firstDay: string, zoom: TimelineZoom): string {
  return addDays(firstDay, DAYS_SHOWN[zoom] - 1);
}

/** "Mon 16 Nov 2026". */
export function formatPlantDate(date: string): string {
  const parts = new Intl.DateTimeFormat(DISPLAY_LOCALE, { timeZone: 'UTC', weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).formatToParts(
    new Date(dateToUtcMs(date)),
  );
  return parts
    .filter((part) => part.type !== 'literal')
    .map((part) => part.value)
    .join(' ');
}

export function isPlantDate(text: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(text) && !Number.isNaN(dateToUtcMs(text)) && new Date(dateToUtcMs(text)).toISOString().startsWith(text);
}

function dateToUtcMs(date: string): number {
  return Date.parse(`${date}T00:00:00Z`);
}
