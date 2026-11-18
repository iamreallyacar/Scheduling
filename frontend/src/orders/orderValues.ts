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
