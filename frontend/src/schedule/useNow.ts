import { useEffect, useState } from 'react';

const NOW_REFRESH_MS = 60_000;

/** The current time, read again every minute, so a render stays pure and the now line moves. */
export function useNow(): number {
  const [nowMs, setNowMs] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => {
      setNowMs(Date.now());
    }, NOW_REFRESH_MS);
    return () => {
      clearInterval(timer);
    };
  }, []);
  return nowMs;
}
