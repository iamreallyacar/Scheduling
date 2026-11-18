import { screen, within } from '@testing-library/react';
import type { UnscheduledLotResponse } from '../api/generated';
import { stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const UNSCHEDULED_LOTS_PATH = '/api/v1/versions/live/unscheduled-lots';
const building = { id: 1, name: 'Building machine' };
const curing = { id: 2, name: 'Curing press' };

// Listed later due date first: the page must keep the order of the answer, not sort it.
const curingLot: UnscheduledLotResponse = {
  id: 52,
  lotNo: 1,
  quantity: 200,
  durationMinutes: 1230,
  orderId: 7,
  orderNo: 'SO-1063',
  dueDate: '2026-11-20',
  sequenceNo: 2,
  resourceType: curing,
  predecessors: [{ sequenceNo: 1, resourceType: building, minLagMinutes: 120 }],
};
const buildingLot: UnscheduledLotResponse = {
  id: 61,
  lotNo: 1,
  quantity: 120,
  durationMinutes: 400,
  orderId: 8,
  orderNo: 'SO-1064',
  dueDate: '2026-11-18',
  sequenceNo: 1,
  resourceType: building,
  predecessors: [],
};

function stubScheduleApi(unscheduledLots: UnscheduledLotResponse[]) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: viewerUser }),
    [`GET ${UNSCHEDULED_LOTS_PATH}`]: () => ({ status: 200, body: unscheduledLots }),
  });
}

describe('schedule page', () => {
  it('AC_175 lists the unscheduled lots of the live version in the order the API returns', async () => {
    stubScheduleApi([curingLot, buildingLot]);
    renderApp('/schedule');

    const queue = await screen.findByRole('table', { name: 'Unscheduled lots (2)' });
    expect(screen.getByText('Version: Live')).toBeInTheDocument();
    const rows = within(queue).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell').map((cell) => cell.textContent))).toEqual([
      ['SO-1063', '2 Curing press', '1', '200', '1230', '2026-11-20', '1 Building machine, lag 120 minutes'],
      ['SO-1064', '1 Building machine', '1', '120', '400', '2026-11-18', '-'],
    ]);
    expect(within(queue).getByRole('link', { name: 'SO-1063' })).toHaveAttribute('href', '/orders/7');
  });

  it('AC_175 says that every lot is placed when none waits', async () => {
    stubScheduleApi([]);
    renderApp('/schedule');

    expect(await screen.findByText('Every lot is placed.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Unscheduled lots (0)' })).toBeInTheDocument();
  });
});
