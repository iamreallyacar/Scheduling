import type { CalendarResponse, CalendarShiftsRequest, DayOfWeek } from '../api/generated';

// The shifts of a calendar as they are being edited. Fields hold what the Admin typed; nothing
// is checked here, the server decides what is valid.
export interface ShiftDraft {
  /** Identifies the shift while editing, so React keeps each row's inputs when rows are removed. */
  key: number;
  weekday: DayOfWeek | '';
  start: string;
  end: string;
}

export interface ShiftsDraft {
  shifts: readonly ShiftDraft[];
  nextKey: number;
}

/** Monday first, as planners read a week. */
export const WEEKDAYS: readonly DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function draftFromCalendar(calendar: CalendarResponse): ShiftsDraft {
  const shifts = calendar.shifts.map((shift, position) => ({ key: position + 1, weekday: shift.weekday, start: shift.start, end: shift.end }));
  return { shifts, nextKey: shifts.length + 1 };
}

export function addShift(draft: ShiftsDraft): ShiftsDraft {
  return { shifts: [...draft.shifts, { key: draft.nextKey, weekday: '', start: '', end: '' }], nextKey: draft.nextKey + 1 };
}

export function removeShift(draft: ShiftsDraft, shiftKey: number): ShiftsDraft {
  return { ...draft, shifts: draft.shifts.filter((shift) => shift.key !== shiftKey) };
}

export function changeShift(draft: ShiftsDraft, shiftKey: number, change: Partial<Omit<ShiftDraft, 'key'>>): ShiftsDraft {
  return { ...draft, shifts: draft.shifts.map((shift) => (shift.key === shiftKey ? { ...shift, ...change } : shift)) };
}

/** What is sent to the server. An empty field is sent as null, so the server reports it. */
export function toShiftsRequest(draft: ShiftsDraft): CalendarShiftsRequest {
  return {
    shifts: draft.shifts.map((shift) => ({
      weekday: shift.weekday === '' ? null : shift.weekday,
      start: textOrNull(shift.start),
      end: textOrNull(shift.end),
    })),
  };
}

function textOrNull(text: string): string | null {
  return text.trim() === '' ? null : text.trim();
}
