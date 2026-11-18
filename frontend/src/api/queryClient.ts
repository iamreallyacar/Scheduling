import { MutationCache, QueryCache, QueryClient, type QueryKey } from '@tanstack/react-query';
import { hasProblemCode, PROBLEM_CODE_AUTH_REQUIRED } from './problem';
import { isCurrentUserQuery, queryKeys } from './queryKeys';

interface AppMutationMeta {
  /** The query of the row the write deletes: it is dropped, because reading it again answers 404. */
  deletedQueryKey?: QueryKey;
  /** Set by sign out: the session is gone, so nothing can be read again, and it clears the cache itself. */
  skipsQueryRefresh?: true;
}

declare module '@tanstack/react-query' {
  interface Register {
    // Readonly makes it a mapped type, which the library accepts as its record of meta values;
    // unknown fields are still rejected.
    mutationMeta: Readonly<AppMutationMeta>;
  }
}

export function createAppQueryClient(): QueryClient {
  // When any call finds the session gone (expired, or signed out elsewhere), the app drops
  // the user, which brings up the sign-in page (AC-6).
  const forgetUserWhenSessionEnded = (error: unknown) => {
    if (hasProblemCode(error, PROBLEM_CODE_AUTH_REQUIRED)) {
      queryClient.setQueryData(queryKeys.currentUser, null);
    }
  };
  // A write can change what any area shows, so every query is read again in this one place
  // (DEC-36, AC-164). The cache runs this before the write's own onSuccess, so a form closes
  // only once the data on screen is fresh. The signed-in user belongs to the session.
  const refreshAfterWrite = async (meta: Readonly<AppMutationMeta> | undefined) => {
    if (meta?.skipsQueryRefresh === true) {
      return;
    }
    if (meta?.deletedQueryKey !== undefined) {
      queryClient.removeQueries({ queryKey: meta.deletedQueryKey });
    }
    await queryClient.invalidateQueries({ predicate: (query) => !isCurrentUserQuery(query) });
  };
  const queryClient = new QueryClient({
    queryCache: new QueryCache({ onError: forgetUserWhenSessionEnded }),
    mutationCache: new MutationCache({
      onError: forgetUserWhenSessionEnded,
      onSuccess: (_data, _variables, _context, mutation) => refreshAfterWrite(mutation.meta),
    }),
    // The API answers rejections deliberately, so asking again would give the same answer.
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return queryClient;
}
