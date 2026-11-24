import { useMutation, useQueryClient } from '@tanstack/react-query';
import { NavLink, Outlet, useLocation } from 'react-router';
import { signOut } from '../api/generated/endpoints.gen';
import { problemOf } from '../api/problem';
import { isCurrentUserQuery, queryKeys } from '../api/queryKeys';
import { forgetAntiforgeryToken } from '../api/request';
import { useSignedInUser } from '../auth/signedInUser';
import { PlantTimeZoneProvider } from '../calendar/PlantTimeZoneProvider';
import { ProblemSummary } from '../forms/ProblemSummary';
import { PageErrorBoundary } from './PageErrorBoundary';

// The frame around every signed-in page: navigation per module area, the user and sign-out.
export function AppShell() {
  const user = useSignedInUser();
  const location = useLocation();
  const queryClient = useQueryClient();
  const signOutMutation = useMutation({
    mutationFn: signOut,
    meta: { skipsQueryRefresh: true },
    onSuccess: () => {
      forgetAntiforgeryToken();
      queryClient.setQueryData(queryKeys.currentUser, null);
      // Nothing loaded for this user may be shown to the next one.
      queryClient.removeQueries({ predicate: (query) => !isCurrentUserQuery(query) });
    },
  });

  return (
    <>
      <header className="app-header">
        <span className="app-title">Tire Production Scheduler</span>
        <nav aria-label="Main">
          <NavLink to="/dashboard">Dashboard</NavLink>
          <NavLink to="/orders">Orders</NavLink>
          <NavLink to="/schedule">Schedule</NavLink>
          <NavLink to="/machines">Machines</NavLink>
          <NavLink to="/calendar">Calendar</NavLink>
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
        {/* Keyed by address, so leaving a failed page by the navigation shows the next page. */}
        <PageErrorBoundary key={location.pathname}>
          <PlantTimeZoneProvider>
            <Outlet />
          </PlantTimeZoneProvider>
        </PageErrorBoundary>
      </main>
    </>
  );
}
