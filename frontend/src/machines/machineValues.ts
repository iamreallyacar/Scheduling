import type { ResourceStatus, UnavailabilityKind } from '../api/generated';

// The values the API uses for machine status and unavailability kind, with their labels.
export const RESOURCE_STATUS_LABELS: Record<ResourceStatus, string> = {
  Down: 'Down',
  Maintenance: 'Maintenance',
  Idle: 'Idle',
};

export const UNAVAILABILITY_KIND_LABELS: Record<UnavailabilityKind, string> = {
  Planned: 'Planned maintenance',
  Breakdown: 'Breakdown',
};

export const UNAVAILABILITY_KINDS: readonly UnavailabilityKind[] = ['Planned', 'Breakdown'];
