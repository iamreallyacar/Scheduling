import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, OrderResponse } from '../api/generated';
import { type FakeApiAnswer, type FakeApiRequest, plannerUser, problem, sentBody, stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';
import { order, passengerTire, summaryOf, truckTire } from './orderSamples';
import { rowOf } from '../testing/rowOf';

const ORDER_PATH = '/api/v1/orders/5';

// The order the API answers with can change during a test, as after a write.
function stubOrderApi(
  currentUser: CurrentUserResponse,
  routes: Record<string, (request: FakeApiRequest) => FakeApiAnswer> = {},
  shownOrder: () => OrderResponse = () => order,
) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    [`GET ${ORDER_PATH}`]: () => ({ status: 200, body: shownOrder() }),
    'GET /api/v1/products': () => ({ status: 200, body: [passengerTire, truckTire] }),
    'GET /api/v1/orders': () => ({ status: 200, body: { items: [summaryOf(shownOrder())], page: 1, pageSize: 50, totalCount: 1 } }),
    ...routes,
  });
}

describe('order page', () => {
  it('AC_61 AC_62 AC_70 shows the order with its progress, operations, lots with status and dependencies, and a Viewer sees no actions', async () => {
    stubOrderApi(viewerUser);
    renderApp('/orders/5');

    expect(await screen.findByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
    expect(screen.getByText('Tire Shop North')).toBeInTheDocument();
    expect(screen.getByText('PS-205 Passenger 205/55 R16')).toBeInTheDocument();
    expect(await screen.findByText('18 Nov 2026, 10:30')).toBeInTheDocument();
    expect(screen.getByText('0 of 202 tires completed')).toBeInTheDocument();
    const operationRow = rowOf(within(screen.getByRole('table', { name: 'Operations' })).getByRole('rowheader', { name: '2' }));
    expect(operationRow.textContent).toBe('2Curing press10130122');
    const lotRow = rowOf(within(screen.getByRole('table', { name: 'Lots' })).getByRole('rowheader', { name: '2' }));
    expect(lotRow.textContent).toBe('21101642Unscheduled');
    expect(within(screen.getByRole('table', { name: 'Dependencies' })).getByRole('row', { name: '1 2 120' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit|Cancel|Delete/ })).not.toBeInTheDocument();
  });

  it('AC_47 AC_147 a Planner changes the customer and the page reads the saved order again', async () => {
    let savedOrder = order;
    const requests = stubOrderApi(
      plannerUser,
      {
        [`PUT ${ORDER_PATH}`]: (request) => {
          savedOrder = { ...order, ...(request.body as Partial<OrderResponse>) };
          return { status: 204 };
        },
      },
      () => savedOrder,
    );
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Edit order' }));
    const form = screen.getByRole('form', { name: 'Edit order SO-5' });
    expect(await within(form).findByLabelText('Product')).toHaveValue('3');
    await user.clear(within(form).getByLabelText('Customer'));
    await user.type(within(form).getByLabelText('Customer'), 'Fleet Service');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Fleet Service')).toBeInTheDocument();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(sentBody(requests, 'PUT', ORDER_PATH)).toEqual({
      orderNo: 'SO-5',
      customerName: 'Fleet Service',
      productId: 3,
      quantity: 101,
      dueDate: '2026-12-01',
      priority: 'High',
    });
  });

  it('AC_49 shows why the quantity could not change', async () => {
    stubOrderApi(plannerUser, {
      [`PUT ${ORDER_PATH}`]: () => problem(409, 'order.locked', 'The product and quantity cannot change while a lot of this order is placed or started (A1).'),
    });
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Edit order' }));
    const form = screen.getByRole('form', { name: 'Edit order SO-5' });
    await user.clear(within(form).getByLabelText('Quantity'));
    await user.type(within(form).getByLabelText('Quantity'), '120');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('cannot change while a lot of this order is placed or started');
    expect(within(form).getByLabelText('Quantity')).toHaveValue(120);
  });

  it('AC_50 AC_147 a Planner cancels the order and the page reads it again with the cancellation time and status', async () => {
    let savedOrder = order;
    const requests = stubOrderApi(
      plannerUser,
      {
        [`POST ${ORDER_PATH}/cancel`]: () => {
          savedOrder = { ...order, cancelledAt: '2026-11-19T13:05:00Z', status: 'Cancelled' };
          return { status: 204 };
        },
      },
      () => savedOrder,
    );
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Cancel order' }));

    expect(await screen.findByText('19 Nov 2026, 14:05')).toBeInTheDocument();
    expect(statusOnPage()).toBe('Cancelled');
    expect(requests.some((request) => request.method === 'POST' && request.path === `${ORDER_PATH}/cancel`)).toBe(true);
  });

  it('AC_148 shows the status the API returns, without a late mark when the order is on time', async () => {
    stubOrderApi(viewerUser);
    renderApp('/orders/5');

    expect(await screen.findByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
    expect(statusOnPage()).toBe('Pending');
    expect(screen.queryByText('Late')).not.toBeInTheDocument();
  });

  it('AC_148 shows a late mark when the API says the order is late', async () => {
    stubOrderApi(viewerUser, {}, () => ({ ...order, status: 'InProgress', isLate: true }));
    renderApp('/orders/5');

    expect(await screen.findByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
    expect(statusOnPage()).toBe('In Progress Late');
  });

  it('AC_51 shows why the order could not be cancelled', async () => {
    stubOrderApi(plannerUser, {
      [`POST ${ORDER_PATH}/cancel`]: () => problem(409, 'order.lot_in_progress', 'This order cannot be cancelled while one of its lots is in progress.'),
    });
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Cancel order' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('cannot be cancelled while one of its lots is in progress');
  });

  it('AC_18 a Planner deletes the order and is taken back to the list', async () => {
    const requests = stubOrderApi(plannerUser, { [`DELETE ${ORDER_PATH}`]: () => ({ status: 204 }) });
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Delete order' }));

    expect(await screen.findByRole('heading', { name: 'Orders' })).toBeInTheDocument();
    expect(requests.some((request) => request.method === 'DELETE' && request.path === ORDER_PATH)).toBe(true);
  });

  it('AC_19 shows why the order could not be deleted', async () => {
    stubOrderApi(plannerUser, {
      [`DELETE ${ORDER_PATH}`]: () => problem(409, 'order.not_deletable', 'Only a pending order with no placed or started lot can be deleted. Cancel this order instead.'),
    });
    const user = userEvent.setup();
    renderApp('/orders/5');

    await user.click(await screen.findByRole('button', { name: 'Delete order' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Cancel this order instead.');
    expect(screen.getByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
  });

  it('AC_71 AC_180 shows each live placement, and a Planner moves or removes it in the placement form', async () => {
    let shownOrder = placedOrder;
    const requests = stubOrderApi(
      plannerUser,
      {
        'GET /api/v1/resources': () => ({
          status: 200,
          body: [{ id: 7, name: 'Press 2', resourceType: { id: 2, name: 'Curing press' }, calendar: { id: 1, name: 'Plant default' }, isActive: true, status: 'Idle' }],
        }),
        'DELETE /api/v1/versions/live/lots/22/placement': () => {
          shownOrder = order;
          return { status: 204 };
        },
      },
      () => shownOrder,
    );
    const user = userEvent.setup();
    renderApp('/orders/5');

    const lots = await screen.findByRole('table', { name: 'Lots' });
    const placedRow = rowOf(within(lots).getByRole('rowheader', { name: '2' }));
    await within(placedRow).findByText('16 Nov 2026, 08:00');
    expect(within(placedRow).getAllByRole('cell').map((cell) => cell.textContent)).toEqual([
      '1',
      '101',
      '642',
      'Scheduled',
      'Press 2',
      '16 Nov 2026, 08:00',
      '16 Nov 2026, 18:42',
      'Move...',
    ]);
    expect(within(rowOf(within(lots).getByRole('rowheader', { name: '1' }))).getByRole('button', { name: 'Place...' })).toBeInTheDocument();

    await user.click(within(placedRow).getByRole('button', { name: 'Move...' }));
    const form = await screen.findByRole('form', { name: 'Move lot - Order SO-5, stage 2 Curing press, lot 1' });
    expect(await within(form).findByLabelText('Machine')).toHaveValue('7');
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-16T08:00');
    await user.click(within(form).getByRole('button', { name: 'Remove placement' }));

    // Both lots are unplaced again in the order the page reads after the removal.
    expect(await within(screen.getByRole('table', { name: 'Lots' })).findAllByRole('button', { name: 'Place...' })).toHaveLength(2);
    expect(screen.queryByRole('form', { name: /^Move lot/ })).not.toBeInTheDocument();
    expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/v1/versions/live/lots/22/placement')).toBe(true);
  });

  it('AC_180 a Viewer sees the placements without place or move actions', async () => {
    stubOrderApi(viewerUser, {}, () => placedOrder);
    renderApp('/orders/5');

    expect(await screen.findByText('Press 2')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Place|Move/ })).not.toBeInTheDocument();
  });

  it('AC_23 an unknown order shows the problem', async () => {
    stubApi({
      'GET /api/v1/me': () => ({ status: 200, body: plannerUser }),
      'GET /api/v1/orders/9': () => problem(404, 'not_found', 'This order does not exist.'),
    });
    renderApp('/orders/9');

    expect(await screen.findByRole('alert')).toHaveTextContent('This order does not exist.');
  });
});

// The curing lot placed on Press 2, Mon 2026-11-16 08:00 to 18:42 in Berlin.
const placedOrder: OrderResponse = {
  ...order,
  status: 'Scheduled',
  operations: order.operations.map((operation) =>
    operation.sequenceNo === 2
      ? {
          ...operation,
          lots: operation.lots.map((lot) => ({
            ...lot,
            status: 'Scheduled' as const,
            placement: { resource: { id: 7, name: 'Press 2' }, plannedStart: '2026-11-16T07:00:00Z', plannedEnd: '2026-11-16T17:42:00Z' },
          })),
        }
      : operation,
  ),
};

function statusOnPage(): string | null {
  return screen.getByText('Status', { selector: 'dt' }).nextElementSibling?.textContent ?? null;
}
