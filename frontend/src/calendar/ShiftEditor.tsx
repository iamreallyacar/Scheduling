import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { replaceCalendarShifts } from '../api/generated/endpoints.gen';
import type { CalendarResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { SelectField, TextField, type SelectOption } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { addShift, changeShift, draftFromCalendar, removeShift, toShiftsRequest, WEEKDAYS, type ShiftsDraft } from './shiftDraft';

const NOTHING_CHOSEN = '';
const weekdayOptions: SelectOption[] = [{ value: NOTHING_CHOSEN, label: 'Choose a day' }, ...WEEKDAYS.map((weekday) => ({ value: weekday, label: weekday }))];

// The shifts of a calendar are edited together and saved as one weekly list (A26). Field names
// are the paths the API uses in `errors`, so each message lands next to its field.
export function ShiftEditor({ calendar }: { calendar: CalendarResponse }) {
  const [draft, setDraft] = useState(() => draftFromCalendar(calendar));
  const saveMutation = useMutation({
    mutationFn: () => replaceCalendarShifts(calendar.id, toShiftsRequest(draft)),
    onSuccess: (savedCalendar) => {
      // Shows the shifts as the server saved them, in its order.
      setDraft(draftFromCalendar(savedCalendar));
    },
  });
  const problem = problemOf(saveMutation.error);

  const edit = (changedDraft: ShiftsDraft) => {
    // The answer to the last save no longer describes what is on screen.
    saveMutation.reset();
    setDraft(changedDraft);
  };

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form onSubmit={submit} noValidate aria-label="Shifts">
      <ProblemSummary problem={problem} />
      {saveMutation.isSuccess && <p role="status">Shifts saved.</p>}
      {draft.shifts.length === 0 && <p className="page-message">No shifts, so this calendar has no working time.</p>}
      {draft.shifts.map((shift, position) => {
        const shiftName = `Shift ${String(position + 1)}`;
        const fieldPath = `shifts[${String(position)}]`;
        return (
          <fieldset key={shift.key} className="list-item shift-item">
            <legend>{shiftName}</legend>
            <SelectField
              label="Weekday"
              name={`${fieldPath}.weekday`}
              value={shift.weekday}
              onChange={(weekday) => {
                edit(changeShift(draft, shift.key, { weekday: WEEKDAYS.find((day) => day === weekday) ?? '' }));
              }}
              problem={problem}
              options={weekdayOptions}
            />
            <TextField
              label="Start (HH:mm)"
              name={`${fieldPath}.start`}
              value={shift.start}
              onChange={(start) => {
                edit(changeShift(draft, shift.key, { start }));
              }}
              problem={problem}
            />
            <TextField
              label="End (HH:mm)"
              name={`${fieldPath}.end`}
              value={shift.end}
              onChange={(end) => {
                edit(changeShift(draft, shift.key, { end }));
              }}
              problem={problem}
            />
            <button
              type="button"
              aria-label={`Remove ${shiftName}`}
              onClick={() => {
                edit(removeShift(draft, shift.key));
              }}
            >
              Remove
            </button>
          </fieldset>
        );
      })}
      <div className="section-actions">
        <button
          type="button"
          onClick={() => {
            edit(addShift(draft));
          }}
        >
          Add shift
        </button>
      </div>
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save shifts
        </button>
      </div>
    </form>
  );
}
