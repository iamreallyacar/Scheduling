import type { ApiProblem } from '../api/problem';

// The `detail` of a rejected request, shown where the user acted (AC-26).
export function ProblemSummary({ problem }: { problem: ApiProblem | null }) {
  if (problem === null) {
    return null;
  }
  return (
    <p role="alert" className="problem">
      {problem.detail}
      {problem.correlationId !== null && <span className="problem-reference"> Reference: {problem.correlationId}</span>}
    </p>
  );
}
