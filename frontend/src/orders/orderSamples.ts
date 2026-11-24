import type { OrderResponse, OrderSummaryResponse, ProductResponse } from '../api/generated';

// Sample answers shared by the order page tests.
export const passengerTire: ProductResponse = { id: 3, code: 'PS-205', name: 'Passenger 205/55 R16', spec: null };
export const truckTire: ProductResponse = { id: 4, code: 'TR-315', name: 'Truck 315/80 R22.5', spec: null };

export const order: OrderResponse = {
  id: 5,
  orderNo: 'SO-5',
  customerName: 'Tire Shop North',
  product: { id: passengerTire.id, code: passengerTire.code, name: passengerTire.name },
  quantity: 101,
  dueDate: '2026-12-01',
  priority: 'High',
  createdAt: '2026-11-18T09:30:00Z',
  cancelledAt: null,
  status: 'Pending',
  isLate: false,
  progress: { completedQuantity: 0, totalQuantity: 202 },
  operations: [
    {
      id: 11,
      sequenceNo: 1,
      resourceType: { id: 1, name: 'Building machine' },
      quantity: 101,
      setupMinutes: 0,
      cycleMinutes: 5,
      unitsPerCycle: 1,
      lots: [{ id: 21, lotNo: 1, quantity: 101, durationMinutes: 505, status: 'Unscheduled', placement: null, actualStart: null, actualEnd: null }],
    },
    {
      id: 12,
      sequenceNo: 2,
      resourceType: { id: 2, name: 'Curing press' },
      quantity: 101,
      setupMinutes: 30,
      cycleMinutes: 12,
      unitsPerCycle: 2,
      lots: [{ id: 22, lotNo: 1, quantity: 101, durationMinutes: 642, status: 'Unscheduled', placement: null, actualStart: null, actualEnd: null }],
    },
  ],
  dependencies: [{ predecessorSequenceNo: 1, successorSequenceNo: 2, minLagMinutes: 120 }],
};

export function summaryOf(fullOrder: OrderResponse): OrderSummaryResponse {
  const { id, orderNo, customerName, product, quantity, dueDate, priority, createdAt, cancelledAt, status, isLate } = fullOrder;
  return { id, orderNo, customerName, product, quantity, dueDate, priority, createdAt, cancelledAt, status, isLate };
}
