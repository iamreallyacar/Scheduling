// Conversions between UTC instants and the plant's wall clock (DEC-40). The API sends and
// receives UTC; the planner reads and types plant-local times. A local date and time is written
// as a date and time field holds it: YYYY-MM-DDTHH:mm.

const MINUTE_MS = 60_000;
const DAY_MS = 24 * 60 * MINUTE_MS;

export function formatPlantTime(utc: string, timeZone: string): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone, dateStyle: 'medium', timeStyle: 'short' }).format(new Date(utc));
}

export function utcToPlantLocal(utc: string, timeZone: string): string {
  return toLocalText(wallClockOf(Date.parse(utc), timeZone));
}

// A local time that occurs twice is its first occurrence, and one inside a DST gap is the first
// time after the gap (A38), as the engine reads them (A27).
export function plantLocalToUtc(local: string, timeZone: string): string {
  const wallClock = parseLocalText(local);
  // Offsets change at most once within a day, so the offsets a day either side are the only candidates.
  const offsetBefore = wallClockOf(wallClock - DAY_MS, timeZone) - (wallClock - DAY_MS);
  const offsetAfter = wallClockOf(wallClock + DAY_MS, timeZone) - (wallClock + DAY_MS);
  // The larger offset gives the earlier instant.
  const earlier = wallClock - Math.max(offsetBefore, offsetAfter);
  const later = wallClock - Math.min(offsetBefore, offsetAfter);
  const existing = [earlier, later].find((instant) => wallClockOf(instant, timeZone) === wallClock);
  if (existing !== undefined) {
    return new Date(existing).toISOString();
  }

  // Inside the gap: the first minute whose wall clock reads at or after the typed time.
  let low = earlier;
  let high = later;
  while (low < high) {
    const middle = low + Math.floor((high - low) / MINUTE_MS / 2) * MINUTE_MS;
    if (wallClockOf(middle, timeZone) >= wallClock) {
      high = middle;
    } else {
      low = middle + MINUTE_MS;
    }
  }
  return new Date(low).toISOString();
}

// The wall clock in the zone at the instant, as milliseconds of a UTC date with the same fields.
function wallClockOf(instant: number, timeZone: string): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(new Date(instant));
  const field = (type: Intl.DateTimeFormatPartTypes): number => {
    const part = parts.find((candidate) => candidate.type === type);
    if (part === undefined) {
      throw new Error(`The time zone format has no ${type}.`);
    }
    return Number(part.value);
  };
  return Date.UTC(field('year'), field('month') - 1, field('day'), field('hour'), field('minute'), field('second'));
}

function parseLocalText(local: string): number {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(local);
  if (match === null) {
    throw new Error(`"${local}" is not a local date and time (YYYY-MM-DDTHH:mm).`);
  }
  const [, year, month, day, hour, minute] = match.map(Number) as [number, number, number, number, number, number];
  return Date.UTC(year, month - 1, day, hour, minute);
}

function toLocalText(wallClock: number): string {
  return new Date(wallClock).toISOString().slice(0, 16);
}
