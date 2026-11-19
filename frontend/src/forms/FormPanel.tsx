import type { ReactNode, SyntheticEvent } from 'react';
import type { ApiProblem } from '../api/problem';
import { ProblemSummary } from './ProblemSummary';

interface FormPanelProps {
  /** The name page tests and screen readers find the form by. */
  accessibleName: string;
  /** The heading above the form; without one the form shows none. */
  heading?: string;
  /** Text between the heading and the problem. */
  description?: ReactNode;
  problem: ApiProblem | null;
  pending: boolean;
  submitLabel: string;
  onSubmit: () => void;
  /** Without a cancel handler the form has no cancel button. */
  onCancel?: () => void;
  cancelLabel?: string;
  /** Buttons between submit and cancel. */
  extraActions?: ReactNode;
  /** A form inside a page section, such as the shifts, draws no panel frame. */
  framed?: boolean;
  children: ReactNode;
}

/** The frame every save form shares: heading, the server's problem, the fields, and the submit and cancel buttons. */
export function FormPanel({
  accessibleName,
  heading,
  description,
  problem,
  pending,
  submitLabel,
  onSubmit,
  onCancel,
  cancelLabel = 'Cancel',
  extraActions,
  framed = true,
  children,
}: FormPanelProps) {
  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    onSubmit();
  };

  return (
    <form className={framed ? 'panel' : undefined} onSubmit={submit} noValidate aria-label={accessibleName}>
      {heading !== undefined && <h3>{heading}</h3>}
      {description}
      <ProblemSummary problem={problem} />
      {children}
      <div className="form-actions">
        <button type="submit" disabled={pending}>
          {submitLabel}
        </button>
        {extraActions}
        {onCancel !== undefined && (
          <button type="button" onClick={onCancel}>
            {cancelLabel}
          </button>
        )}
      </div>
    </form>
  );
}
