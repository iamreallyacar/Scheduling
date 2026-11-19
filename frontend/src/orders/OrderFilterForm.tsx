import { useState, type SyntheticEvent } from 'react';
import type { ApiProblem } from '../api/problem';
import { CheckboxField, SelectField, TextField, type SelectOption } from '../forms/Fields';
import type { OrderListQuery } from './orderListQuery';
import { ORDER_PRIORITIES, ORDER_STATUS_LABELS, ORDER_STATUSES } from './orderValues';

interface OrderFilterFormProps {
  /** The query the list shows now; the form starts from it. */
  query: OrderListQuery;
  /** The list's problem, so a rejected value is shown next to its field (AC-149). */
  problem: ApiProblem | null;
  /** Gets the new query, without a page, so the list starts again at page 1. */
  onApply: (query: OrderListQuery) => void;
}

const DESCENDING_PREFIX = '-';
const ANY = '';

// Shown when the address names no sort.
const DEFAULT_SORT_FIELD = 'dueDate';
// The list item fields the server sorts by (AC-68).
const sortFieldOptions: SelectOption[] = [
  { value: DEFAULT_SORT_FIELD, label: 'Due date' },
  { value: 'orderNo', label: 'Order number' },
  { value: 'customerName', label: 'Customer' },
  { value: 'priority', label: 'Priority' },
  { value: 'createdAt', label: 'Creation time' },
];
const directionOptions: SelectOption[] = [
  { value: 'ascending', label: 'Ascending' },
  { value: 'descending', label: 'Descending' },
];
const lateOptions: SelectOption[] = [
  { value: ANY, label: 'Any' },
  { value: 'true', label: 'Late only' },
  { value: 'false', label: 'Not late' },
];

// F-ORD-4: search, filters and sort of the order list. It only collects values; the server
// filters and sorts (AC-150).
export function OrderFilterForm({ query, problem, onApply }: OrderFilterFormProps) {
  const [search, setSearch] = useState(query.search ?? '');
  const [customer, setCustomer] = useState(query.customer ?? '');
  const [statuses, setStatuses] = useState<readonly string[]>(query.status ?? []);
  const [late, setLate] = useState(query.late === undefined ? ANY : String(query.late));
  const [dueFrom, setDueFrom] = useState(query.dueFrom ?? '');
  const [dueTo, setDueTo] = useState(query.dueTo ?? '');
  const [priorities, setPriorities] = useState<readonly string[]>(query.priority ?? []);
  const [sortField, setSortField] = useState(query.sort?.replace(DESCENDING_PREFIX, '') ?? DEFAULT_SORT_FIELD);
  const [isDescending, setIsDescending] = useState(query.sort?.startsWith(DESCENDING_PREFIX) ?? false);

  function apply(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const applied: OrderListQuery = { sort: isDescending ? `${DESCENDING_PREFIX}${sortField}` : sortField };
    if (search !== '') {
      applied.search = search;
    }
    if (statuses.length > 0) {
      applied.status = [...statuses];
    }
    if (late !== ANY) {
      applied.late = late === 'true';
    }
    if (customer !== '') {
      applied.customer = customer;
    }
    if (dueFrom !== '') {
      applied.dueFrom = dueFrom;
    }
    if (dueTo !== '') {
      applied.dueTo = dueTo;
    }
    if (priorities.length > 0) {
      applied.priority = [...priorities];
    }
    onApply(applied);
  }

  return (
    <form aria-label="Order filters" className="order-filters" onSubmit={apply}>
      <TextField label="Search" name="search" problem={problem} value={search} onChange={setSearch} />
      <TextField label="Customer" name="customer" problem={problem} value={customer} onChange={setCustomer} />
      <ChoiceGroup legend="Status" name="status" problem={problem} values={ORDER_STATUSES} labels={ORDER_STATUS_LABELS} chosen={statuses} onChange={setStatuses} />
      <SelectField label="Late" name="late" problem={problem} value={late} onChange={setLate} options={lateOptions} />
      <TextField label="Due from" name="dueFrom" type="date" problem={problem} value={dueFrom} onChange={setDueFrom} />
      <TextField label="Due to" name="dueTo" type="date" problem={problem} value={dueTo} onChange={setDueTo} />
      <ChoiceGroup legend="Priority" name="priority" problem={problem} values={ORDER_PRIORITIES} chosen={priorities} onChange={setPriorities} />
      <SelectField label="Sort by" name="sort" problem={problem} value={sortField} onChange={setSortField} options={sortFieldOptions} />
      <SelectField
        label="Direction"
        name="direction"
        problem={problem}
        value={isDescending ? 'descending' : 'ascending'}
        onChange={(direction) => {
          setIsDescending(direction === 'descending');
        }}
        options={directionOptions}
      />
      <div className="row-actions">
        <button type="submit">Apply</button>
        <button
          type="button"
          onClick={() => {
            onApply({});
          }}
        >
          Clear
        </button>
      </div>
    </form>
  );
}

interface ChoiceGroupProps<TValue extends string> {
  legend: string;
  name: string;
  problem: ApiProblem | null;
  values: readonly TValue[];
  labels?: Record<TValue, string>;
  chosen: readonly string[];
  onChange: (chosen: readonly string[]) => void;
}

// The server names the whole list in a field error, so its messages are shown once for the group.
function ChoiceGroup<TValue extends string>({ legend, name, problem, values, labels, chosen, onChange }: ChoiceGroupProps<TValue>) {
  const messages = problem?.errors[name] ?? [];
  return (
    <fieldset aria-invalid={messages.length > 0}>
      <legend>{legend}</legend>
      {values.map((value) => (
        <CheckboxField
          key={value}
          label={labels?.[value] ?? value}
          name={name}
          problem={null}
          checked={chosen.includes(value)}
          onChange={(checked) => {
            // Kept in the order of the values, so the address does not depend on the click order.
            onChange(values.filter((candidate) => (candidate === value ? checked : chosen.includes(candidate))));
          }}
        />
      ))}
      {messages.length > 0 && (
        <ul className="field-errors">
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </fieldset>
  );
}
