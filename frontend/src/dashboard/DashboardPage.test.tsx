import { screen, within } from '@testing-library/react';
import type { DashboardResponse, OrderSummaryResponse, ResourceViewResponse } from '../api/generated';
import { problem, stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';
import { rowOf } from '../testing/rowOf';
import { order, summaryOf } from '../orders/orderSamples';

const overdueOrder: OrderSummaryResponse = { ...summaryOf(order), id: 7, orderNo: 'SO-7', dueDate: '2026-11-20', status: 'InProgress', isLate: true };
const dueSoonOrder: OrderSummaryResponse = { ...summaryOf(order), id: 8, orderNo: 'SO-8', customerName: 'Fleet Service', dueDate: '2026-11-25', status: 'Scheduled', isLate: false };
const laterDueSoonOrder: OrderSummaryResponse = { ...summaryOf(order), id: 6, orderNo: 'SO-6', dueDate: '2026-11-30', status: 'Pending', isLate: false };
const downPress: ResourceViewResponse = {
  id: 31,
  name: 'Press 1',
  resourceType: { id: 2, name: 'Curing press' },
  calendar: { id: 1, name: 'Plant default' },
  isActive: true,
  status: 'Down',
};

const dashboard: DashboardResponse = {
  dueSoonUntil: '2026-12-01',
  overdueOrders: [overdueOrder],
  // Not in order of id or number, so the page must keep the API's order.
  dueSoonOrders: [dueSoonOrder, laterDueSoonOrder],
  unscheduledLotCount: 12,
  conflictedPlacementCount: 1,
  downMachines: [downPress],
};

function stubDashboardApi(answer: () => { status: number; body?: unknown }) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: viewerUser }),
    'GET /api/v1/versions/live/dashboard': answer,
  });
}

describe('dashboard', () => {
  it('AC_221 shows the overdue and due-soon orders, the schedule counts and the machines down as the API returns them', async () => {
    stubDashboardApi(() => ({ status: 200, body: dashboard }));
    renderApp('/dashboard');

    const overdue = await screen.findByRole('table', { name: 'Overdue orders' });
    const overdueRow = rowOf(within(overdue).getByRole('rowheader', { name: 'SO-7' }));
    expect(within(overdueRow).getByRole('link', { name: 'SO-7' })).toHaveAttribute('href', '/orders/7');
    expect(overdueRow).toHaveTextContent('Tire Shop North');
    expect(overdueRow).toHaveTextContent('PS-205');
    expect(overdueRow).toHaveTextContent('2026-11-20');
    expect(within(overdueRow).getByText('In Progress')).toBeInTheDocument();
    expect(within(overdueRow).getByText('Late')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Overdue orders (1)' })).toBeInTheDocument();

    expect(screen.getByRole('heading', { name: 'Due by 2026-12-01 (2)' })).toBeInTheDocument();
    const dueSoon = screen.getByRole('table', { name: 'Orders due by 2026-12-01' });
    expect(within(dueSoon).getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['SO-8', 'SO-6']);
    const dueSoonRow = rowOf(within(dueSoon).getByRole('rowheader', { name: 'SO-8' }));
    expect(dueSoonRow).toHaveTextContent('Fleet Service');
    expect(within(dueSoonRow).getByText('Scheduled')).toBeInTheDocument();
    expect(within(dueSoonRow).queryByText('Late')).not.toBeInTheDocument();

    expect(screen.getByRole('link', { name: '12 unscheduled lots' })).toHaveAttribute('href', '/schedule');
    expect(screen.getByRole('link', { name: '1 conflicted placement' })).toHaveAttribute('href', '/schedule');

    const machines = screen.getByRole('table', { name: 'Machines down' });
    const pressRow = rowOf(within(machines).getByRole('rowheader', { name: 'Press 1' }));
    expect(within(pressRow).getByRole('link', { name: 'Press 1' })).toHaveAttribute('href', '/machines/31');
    expect(pressRow).toHaveTextContent('Curing press');
  });

  it('AC_221 lists the dashboard first in the main navigation', async () => {
    stubDashboardApi(() => ({ status: 200, body: dashboard }));
    renderApp('/dashboard');

    const navigation = await screen.findByRole('navigation', { name: 'Main' });
    const firstLink = within(navigation).getAllByRole('link')[0];
    expect(firstLink).toHaveTextContent('Dashboard');
    expect(firstLink).toHaveAttribute('href', '/dashboard');
  });

  it('AC_221 says when a part is empty', async () => {
    stubDashboardApi(() => ({
      status: 200,
      body: { ...dashboard, overdueOrders: [], dueSoonOrders: [], unscheduledLotCount: 0, conflictedPlacementCount: 0, downMachines: [] },
    }));
    renderApp('/dashboard');

    expect(await screen.findByText('No overdue orders.')).toBeInTheDocument();
    expect(screen.getByText('No orders due by 2026-12-01.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '0 unscheduled lots' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '0 conflicted placements' })).toBeInTheDocument();
    expect(screen.getByText('No machine is down.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('AC_221 shows the problem when the read fails', async () => {
    stubDashboardApi(() => problem(500, 'server.error', 'Something went wrong.'));
    renderApp('/dashboard');

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong.');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});
