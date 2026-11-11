export const SIGN_IN_PATH = '/sign-in';

/** Router state passed to the sign-in page: where to go after signing in. */
export interface SignInLocationState {
  returnTo: string;
}

export function returnPathFrom(locationState: unknown): string {
  if (typeof locationState === 'object' && locationState !== null && 'returnTo' in locationState && typeof locationState.returnTo === 'string') {
    return locationState.returnTo;
  }
  return '/';
}
