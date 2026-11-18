import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createHoliday } from '../api/generated/endpoints.gen';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';

export function HolidayForm() {
  const [date, setDate] = useState('');
  const [name, setName] = useState('');
  const saveMutation = useMutation({
    mutationFn: () => createHoliday({ date: date === '' ? null : date, name }),
    onSuccess: () => {
      setDate('');
      setName('');
    },
  });
  const problem = problemOf(saveMutation.error);

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label="Add holiday">
      <h3>Add holiday</h3>
      <ProblemSummary problem={problem} />
      <TextField label="Date" name="date" type="date" value={date} onChange={setDate} problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Add holiday
        </button>
      </div>
    </form>
  );
}
