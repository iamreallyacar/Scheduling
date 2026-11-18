import { Navigate, Outlet, useLocation } from 'react-router';
import { problemOf } from '../api/problem';
import { ProblemSummary } from '../forms/ProblemSummary';
import { SignedInUserContext } from './signedInUser';
import { SIGN_IN_PATH, type SignInLocationState } from './signInLocation';
import { useCurrentUser } from './useCurrentUser';

// Shows the pages inside it only with a session; otherwise the sign-in page, which returns
// to the requested page afterwards (AC-6).
export function RequireSession() {
  const currentUser = useCurrentUser();
  const location = useLocation();

  if (currentUser.isPending) {
    return <p className="page-message">Loading…</p>;
  }
  if (currentUser.isError) {
    return <ProblemSummary problem={problemOf(currentUser.error)} />;
  }
  if (currentUser.data === null) {
    const state: SignInLocationState = { returnTo: location.pathname + location.search };
    return <Navigate to={SIGN_IN_PATH} state={state} replace />;
  }
  return (
    <SignedInUserContext value={currentUser.data}>
      <Outlet />
    </SignedInUserContext>
  );
}
