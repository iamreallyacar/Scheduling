import { useQuery } from '@tanstack/react-query';
import { getCurrentUser } from '../api/endpoints';
import type { CurrentUserResponse } from '../api/generated';
import { hasProblemCode, PROBLEM_CODE_AUTH_REQUIRED } from '../api/problem';
import { currentUserQueryKey } from '../api/queryClient';

/** The signed-in user, or null when there is no session. */
export function useCurrentUser() {
  return useQuery({ queryKey: currentUserQueryKey, queryFn: fetchCurrentUserOrNull, staleTime: Infinity });
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
