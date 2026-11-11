import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Navigate, useLocation } from 'react-router';
import { signIn } from '../api/endpoints';
import { problemOf } from '../api/problem';
import { currentUserQueryKey } from '../api/queryClient';
import { forgetAntiforgeryToken } from '../api/request';
import { TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { returnPathFrom } from './signInLocation';
import { useCurrentUser } from './useCurrentUser';

export function SignInPage() {
  const queryClient = useQueryClient();
  const location = useLocation();
  const currentUser = useCurrentUser();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const signInMutation = useMutation({
    mutationFn: signIn,
    onSuccess: (user) => {
      forgetAntiforgeryToken();
      queryClient.setQueryData(currentUserQueryKey, user);
    },
  });
  const problem = problemOf(signInMutation.error);

  if (currentUser.data) {
    return <Navigate to={returnPathFrom(location.state)} replace />;
  }

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    signInMutation.mutate({ email, password });
  };

  return (
    <main className="sign-in">
      <h1>Tire Production Scheduler</h1>
      <form onSubmit={submit} noValidate>
        <h2>Sign in</h2>
        <ProblemSummary problem={problem} />
        <TextField label="Email" name="email" type="email" autoComplete="username" value={email} onChange={setEmail} problem={problem} />
        <TextField label="Password" name="password" type="password" autoComplete="current-password" value={password} onChange={setPassword} problem={problem} />
        <button type="submit" disabled={signInMutation.isPending}>
          Sign in
        </button>
      </form>
    </main>
  );
}
