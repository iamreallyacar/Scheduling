import type { UseQueryResult } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { problemOf } from '../api/problem';
import { ProblemSummary } from './ProblemSummary';

// Shows loaded data, and a loading line or the problem until there is data.
export function QueryResult<TData>({ query, children }: { query: UseQueryResult<TData>; children: (data: TData) => ReactNode }) {
  if (query.isPending) {
    return <p className="page-message">Loading…</p>;
  }
  if (query.isError) {
    return <ProblemSummary problem={problemOf(query.error)} />;
  }
  return children(query.data);
}
