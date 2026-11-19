import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createHoliday } from '../api/generated/endpoints.gen';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { FormPanel } from '../forms/FormPanel';

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

  return (
    <FormPanel
      accessibleName="Add holiday"
      heading="Add holiday"
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Add holiday"
      onSubmit={() => {
        saveMutation.mutate();
      }}
    >
      <TextField label="Date" name="date" type="date" value={date} onChange={setDate} problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
    </FormPanel>
  );
}
