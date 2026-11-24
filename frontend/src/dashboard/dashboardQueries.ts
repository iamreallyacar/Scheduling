import { useQuery } from '@tanstack/react-query';
import { getDashboard } from '../api/generated/endpoints.gen';
import { queryKeys } from '../api/queryKeys';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';

export function useDashboard() {
  return useQuery({ queryKey: queryKeys.dashboard(SCHEDULE_VERSION), queryFn: () => getDashboard(SCHEDULE_VERSION) });
}
