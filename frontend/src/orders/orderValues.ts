import type { LotStatus, OrderPriority, OrderStatus } from '../api/generated';

// The values the API uses for order status and priority, in the order the page lists them.
export const ORDER_STATUSES: readonly OrderStatus[] = ['Pending', 'Scheduled', 'InProgress', 'Completed', 'Cancelled'];

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  Pending: 'Pending',
  Scheduled: 'Scheduled',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const LOT_STATUS_LABELS: Record<LotStatus, string> = {
  Unscheduled: 'Unscheduled',
  Scheduled: 'Scheduled',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const ORDER_PRIORITIES: readonly OrderPriority[] = ['Low', 'Normal', 'High'];

// The select offers only the priorities, so another value is a bug, not a missing choice.
export function priorityOfOption(optionValue: string): OrderPriority {
  const priority = ORDER_PRIORITIES.find((candidate) => candidate === optionValue);
  if (priority === undefined) {
    throw new Error(`The priority select offered "${optionValue}", which is no priority.`);
  }
  return priority;
}

/** Said for a placed lot whose answer allows no place or move: it has started and stays on its machine (R8, DEC-63, DEC-70). */
export const STARTED_LOT_NOTE = 'Started, so it stays on its machine.';
