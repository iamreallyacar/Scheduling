import { clientFieldProblem, type ApiProblem } from '../api/problem';
import { isPlantLocalText, plantLocalToUtc, utcToPlantLocal } from '../calendar/plantClock';

export const UNREADABLE_TIME_MESSAGE = 'Enter a date and time the page can read, with a four-digit year.';

export type PlantTimeReading<Field extends string> =
  | { problem: ApiProblem }
  | { problem: null; utc: Record<Field, string | null> };

/**
 * Converts typed plant-local times, keyed by their field names, to UTC at the moment of a submit
 * or button press (AC-228). An empty text is null, so the server reports it as missing; a text
 * the page cannot read gives a problem for its field and no times. A field still showing the text
 * of its stored UTC time keeps that time, because the text of a repeated autumn hour does not say
 * which of its two instants it was (AC-230).
 */
export function readPlantTimes<Field extends string>(
  texts: Record<Field, string>,
  timeZone: string,
  storedUtc: Partial<Record<Field, string | null>> = {},
): PlantTimeReading<Field> {
  // Object.keys loses the key type; the keys of Record<Field, string> are exactly Field.
  const fields = Object.keys(texts) as Field[];
  const unreadable = fields.filter((field) => texts[field] !== '' && !isPlantLocalText(texts[field]));
  if (unreadable.length > 0) {
    return { problem: clientFieldProblem(Object.fromEntries(unreadable.map((field) => [field, [UNREADABLE_TIME_MESSAGE]]))) };
  }
  const keptStoredTime = (field: Field): string | null => {
    const stored = storedUtc[field];
    return stored != null && texts[field] === utcToPlantLocal(stored, timeZone) ? new Date(stored).toISOString() : null;
  };
  const utc = Object.fromEntries(fields.map((field) => [field, texts[field] === '' ? null : keptStoredTime(field) ?? plantLocalToUtc(texts[field], timeZone)])) as Record<Field, string | null>;
  return { problem: null, utc };
}
