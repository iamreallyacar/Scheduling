import { createContext, useContext } from 'react';
import type { CurrentUserResponse } from '../api/generated';

export const SignedInUserContext = createContext<CurrentUserResponse | null>(null);

/** For pages behind RequireSession, where a user always exists. */
export function useSignedInUser(): CurrentUserResponse {
  const user = useContext(SignedInUserContext);
  if (user === null) {
    throw new Error('useSignedInUser is used outside RequireSession.');
  }
  return user;
}
