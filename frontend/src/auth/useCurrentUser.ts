import { useQuery } from '@tanstack/react-query';
import { getCurrentUser } from '../api/generated/endpoints.gen';
import type { CurrentUserResponse } from '../api/generated';
import { hasProblemCode, PROBLEM_CODE_AUTH_REQUIRED } from '../api/problem';
import { queryKeys } from '../api/queryKeys';

/** The signed-in user, or null when there is no session. */
export function useCurrentUser() {
  return useQuery({ queryKey: queryKeys.currentUser, queryFn: fetchCurrentUserOrNull, staleTime: Infinity });
}

async function fetchCurrentUserOrNull(): Promise<CurrentUserResponse | null> {
  try {
    return await getCurrentUser();
  } catch (error) {
    if (hasProblemCode(error, PROBLEM_CODE_AUTH_REQUIRED)) {
      return null;
    }
    throw error;
  }
}
