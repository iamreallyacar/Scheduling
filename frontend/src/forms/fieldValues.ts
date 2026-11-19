/** The value of a select's first option, which stands for "nothing chosen yet". */
export const NOTHING_CHOSEN = '';

/** A number field or select holds text. An empty one is sent as null, so the server reports the missing field instead of the form guessing a value. */
export function numberOrNullIfEmpty(text: string): number | null {
  return text.trim() === '' ? null : Number(text);
}
