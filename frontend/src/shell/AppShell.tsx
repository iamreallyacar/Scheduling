import { useMutation, useQueryClient } from '@tanstack/react-query';
import { NavLink, Outlet } from 'react-router';
import { signOut } from '../api/endpoints';
import { problemOf } from '../api/problem';
import { currentUserQueryKey } from '../api/queryClient';
import { forgetAntiforgeryToken } from '../api/request';
import { useSignedInUser } from '../auth/signedInUser';
import { ProblemSummary } from '../forms/ProblemSummary';

// The frame around every signed-in page: navigation per module area, the user and sign-out.
export function AppShell() {
  const user = useSignedInUser();
  const queryClient = useQueryClient();
  const signOutMutation = useMutation({
    mutationFn: signOut,
    onSuccess: () => {
      forgetAntiforgeryToken();
      queryClient.setQueryData(currentUserQueryKey, null);
      // Nothing loaded for this user may be shown to the next one.
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== currentUserQueryKey[0] });
    },
  });

  return (
    <>
      <header className="app-header">
        <span className="app-title">Tire Production Scheduler</span>
        <nav aria-label="Main">
          <NavLink to="/machines">Machines</NavLink>
          <NavLink to="/products">Products</NavLink>
        </nav>
        <span className="app-user">
          {user.name} ({user.role})
        </span>
        <button
          type="button"
          disabled={signOutMutation.isPending}
          onClick={() => {
            signOutMutation.mutate();
          }}
        >
          Sign out
        </button>
      </header>
      <main>
        <ProblemSummary problem={problemOf(signOutMutation.error)} />
        <Outlet />
      </main>
    </>
  );
}
