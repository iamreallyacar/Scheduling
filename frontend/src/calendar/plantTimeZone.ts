import { createContext, useContext } from 'react';

export const PlantTimeZoneContext = createContext<string | null>(null);

/** For pages behind PlantTimeZoneProvider, which waits until the zone is known. */
export function usePlantTimeZone(): string {
  const timeZone = useContext(PlantTimeZoneContext);
  if (timeZone === null) {
    throw new Error('usePlantTimeZone is used outside PlantTimeZoneProvider.');
  }
  return timeZone;
}
