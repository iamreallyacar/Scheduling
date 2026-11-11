import { useId, type HTMLInputAutoCompleteAttribute, type ReactNode } from 'react';
import type { ApiProblem } from '../api/problem';

// Form fields that show the server's messages for their field next to it (AC-26). `name` is
// the field name the API uses in `errors`.
interface FieldProps {
  label: string;
  name: string;
  problem: ApiProblem | null;
}

interface TextFieldProps extends FieldProps {
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'password';
  autoComplete?: HTMLInputAutoCompleteAttribute;
}

export function TextField({ label, name, problem, value, onChange, type = 'text', autoComplete = 'off' }: TextFieldProps) {
  return (
    <Field label={label} name={name} problem={problem}>
      {(inputProps) => (
        <input
          {...inputProps}
          type={type}
          name={name}
          autoComplete={autoComplete}
          value={value}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        />
      )}
    </Field>
  );
}

export interface SelectOption {
  value: string;
  label: string;
}

interface SelectFieldProps extends FieldProps {
  value: string;
  onChange: (value: string) => void;
  options: readonly SelectOption[];
}

export function SelectField({ label, name, problem, value, onChange, options }: SelectFieldProps) {
  return (
    <Field label={label} name={name} problem={problem}>
      {(inputProps) => (
        <select
          {...inputProps}
          name={name}
          value={value}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        >
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </Field>
  );
}

interface CheckboxFieldProps extends FieldProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

export function CheckboxField({ label, name, problem, checked, onChange }: CheckboxFieldProps) {
  return (
    <Field label={label} name={name} problem={problem} inline>
      {(inputProps) => (
        <input
          {...inputProps}
          type="checkbox"
          name={name}
          checked={checked}
          onChange={(event) => {
            onChange(event.target.checked);
          }}
        />
      )}
    </Field>
  );
}

interface InputAccessibilityProps {
  id: string;
  'aria-invalid': boolean;
  'aria-describedby'?: string;
}

interface FieldLayoutProps extends FieldProps {
  inline?: boolean;
  children: (inputProps: InputAccessibilityProps) => ReactNode;
}

function Field({ label, name, problem, inline = false, children }: FieldLayoutProps) {
  const inputId = useId();
  const errorsId = `${inputId}-errors`;
  const messages = problem?.errors[name] ?? [];
  const inputProps: InputAccessibilityProps = { id: inputId, 'aria-invalid': messages.length > 0 };
  if (messages.length > 0) {
    inputProps['aria-describedby'] = errorsId;
  }

  return (
    <div className={inline ? 'field field-inline' : 'field'}>
      <label htmlFor={inputId}>{label}</label>
      {children(inputProps)}
      {messages.length > 0 && (
        <ul id={errorsId} className="field-errors">
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
