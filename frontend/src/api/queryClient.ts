import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import { hasProblemCode, PROBLEM_CODE_AUTH_REQUIRED } from './problem';

export const currentUserQueryKey = ['current-user'] as const;

export function createAppQueryClient(): QueryClient {
  // When any call finds the session gone (expired, or signed out elsewhere), the app drops
  // the user, which brings up the sign-in page (AC-6).
  const forgetUserWhenSessionEnded = (error: unknown) => {
    if (hasProblemCode(error, PROBLEM_CODE_AUTH_REQUIRED)) {
      queryClient.setQueryData(currentUserQueryKey, null);
    }
  };
  const queryClient = new QueryClient({
    queryCache: new QueryCache({ onError: forgetUserWhenSessionEnded }),
    mutationCache: new MutationCache({ onError: forgetUserWhenSessionEnded }),
    // The API answers rejections deliberately, so asking again would give the same answer.
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return queryClient;
}
