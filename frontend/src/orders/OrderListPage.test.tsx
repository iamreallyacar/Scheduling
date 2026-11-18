import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, OrderPageResponse, OrderRequest } from '../api/generated';
import { adminUser, plannerUser, problem, stubApi, viewerUser, type FakeApiAnswer, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';
import { order, passengerTire, plantCalendar, summaryOf, truckTire } from './orderSamples';

type OrderPageAnswer = OrderPageResponse | ((request: FakeApiRequest) => OrderPageResponse);

function stubOrderListApi(currentUser: CurrentUserResponse, page: OrderPageAnswer, createOrder?: (request: FakeApiRequest) => FakeApiAnswer) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/orders': (request) => ({ status: 200, body: typeof page === 'function' ? page(request) : page }),
    'GET /api/v1/products': () => ({ status: 200, body: [passengerTire, truckTire] }),
    'GET /api/v1/orders/5': () => ({ status: 200, body: order }),
    'GET /api/v1/calendars': () => ({ status: 200, body: [plantCalendar] }),
    ...(createOrder === undefined ? {} : { 'POST /api/v1/orders': createOrder }),
  });
}

const onePage: OrderPageResponse = { items: [summaryOf(order)], page: 1, pageSize: 50, totalCount: 1 };

function orderListRequests(requests: FakeApiRequest[]) {
  return requests.filter((request) => request.method === 'GET' && request.path === '/api/v1/orders');
}

describe('order list', () => {
  it('lists orders with order number, customer, product, quantity, due date and priority', async () => {
    stubOrderListApi(viewerUser, onePage);
    renderApp('/orders');

    const row = (await screen.findByRole('rowheader', { name: 'SO-5' })).closest('tr') as HTMLElement;
    expect(within(row).getByRole('link', { name: 'SO-5' })).toHaveAttribute('href', '/orders/5');
    expect(row).toHaveTextContent('Tire Shop North');
    expect(row).toHaveTextContent('PS-205');
    expect(row).toHaveTextContent('101');
    expect(row).toHaveTextContent('2026-12-01');
    expect(row).toHaveTextContent('High');
  });

  it('AC_150 shows the status and a Late mark the server returns', async () => {
    stubOrderListApi(viewerUser, { ...onePage, items: [{ ...summaryOf(order), status: 'InProgress', isLate: true }] });
    renderApp('/orders');

    const row = (await screen.findByRole('rowheader', { name: 'SO-5' })).closest('tr') as HTMLElement;
    expect(within(row).getByText('In Progress')).toBeInTheDocument();
    expect(within(row).getByText('Late')).toBeInTheDocument();
  });

  it('AC_150 sends the search text, filters and sort to the server and shows what it returns', async () => {
    const matchingOrder = { ...summaryOf(order), id: 6, orderNo: 'SO-6' };
    const requests = stubOrderListApi(viewerUser, (request) =>
      request.query.has('search') ? { ...onePage, items: [matchingOrder] } : onePage,
    );
    const user = userEvent.setup();
    renderApp('/orders');

    await screen.findByRole('rowheader', { name: 'SO-5' });
    const filters = screen.getByRole('form', { name: 'Order filters' });
    await user.type(within(filters).getByLabelText('Search'), 'north');
    await user.type(within(filters).getByLabelText('Customer'), 'shop');
    await user.click(within(filters).getByRole('checkbox', { name: 'Pending' }));
    await user.click(within(filters).getByRole('checkbox', { name: 'Cancelled' }));
    await user.selectOptions(within(filters).getByLabelText('Late'), 'Late only');
    await user.type(within(filters).getByLabelText('Due from'), '2026-11-01');
    await user.type(within(filters).getByLabelText('Due to'), '2026-11-30');
    await user.click(within(filters).getByRole('checkbox', { name: 'High' }));
    await user.selectOptions(within(filters).getByLabelText('Sort by'), 'Customer');
    await user.selectOptions(within(filters).getByLabelText('Direction'), 'Descending');
    await user.click(within(filters).getByRole('button', { name: 'Apply' }));

    expect(await screen.findByRole('rowheader', { name: 'SO-6' })).toBeInTheDocument();
    expect(screen.queryByRole('rowheader', { name: 'SO-5' })).not.toBeInTheDocument();
    const sent = orderListRequests(requests).at(-1)?.query;
    expect(sent?.get('search')).toBe('north');
    expect(sent?.get('customer')).toBe('shop');
    expect(sent?.getAll('status')).toEqual(['Pending', 'Cancelled']);
    expect(sent?.get('late')).toBe('true');
    expect(sent?.get('dueFrom')).toBe('2026-11-01');
    expect(sent?.get('dueTo')).toBe('2026-11-30');
    expect(sent?.getAll('priority')).toEqual(['High']);
    expect(sent?.get('sort')).toBe('-customerName');
    expect(sent?.has('page')).toBe(false);
  });

  it('AC_150 opens with the filters in the address and clears them', async () => {
    const requests = stubOrderListApi(viewerUser, onePage);
    const user = userEvent.setup();
    renderApp('/orders?search=north&status=Cancelled&late=false&sort=-dueDate&page=2');

    await screen.findByRole('rowheader', { name: 'SO-5' });
    const filters = screen.getByRole('form', { name: 'Order filters' });
    expect(within(filters).getByLabelText('Search')).toHaveValue('north');
    expect(within(filters).getByRole('checkbox', { name: 'Cancelled' })).toBeChecked();
    expect(within(filters).getByLabelText('Late')).toHaveDisplayValue('Not late');
    expect(within(filters).getByLabelText('Sort by')).toHaveDisplayValue('Due date');
    expect(within(filters).getByLabelText('Direction')).toHaveDisplayValue('Descending');
    expect(orderListRequests(requests).at(-1)?.query.toString()).toBe('page=2&search=north&status=Cancelled&late=false&sort=-dueDate');

    await user.click(within(filters).getByRole('button', { name: 'Clear' }));

    await waitFor(() => {
      expect(orderListRequests(requests).at(-1)?.query.toString()).toBe('');
    });
    const clearedFilters = screen.getByRole('form', { name: 'Order filters' });
    expect(within(clearedFilters).getByLabelText('Search')).toHaveValue('');
    expect(within(clearedFilters).getByRole('checkbox', { name: 'Cancelled' })).not.toBeChecked();
  });

  it('AC_65 AC_150 shows the page and total count the server returns and asks for the next and previous page', async () => {
    const requests = stubOrderListApi(viewerUser, (request) => ({ ...onePage, page: Number(request.query.get('page') ?? '1'), totalCount: 120 }));
    const user = userEvent.setup();
    renderApp('/orders?status=Pending');

    expect(await screen.findByText('Page 1 of 3, 120 orders')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('Page 2 of 3, 120 orders')).toBeInTheDocument();
    const sent = orderListRequests(requests).at(-1)?.query;
    expect(sent?.get('page')).toBe('2');
    expect(sent?.get('status')).toBe('Pending');
    await user.click(screen.getByRole('button', { name: 'Previous page' }));
    expect(await screen.findByText('Page 1 of 3, 120 orders')).toBeInTheDocument();
  });

  it('AC_150 says when no order matches the filters', async () => {
    stubOrderListApi(viewerUser, { items: [], page: 1, pageSize: 50, totalCount: 0 });
    renderApp('/orders?search=nothing');

    expect(await screen.findByText('No orders match these filters.')).toBeInTheDocument();
  });

  it('AC_149 shows the problem when the server rejects the filters in the address', async () => {
    stubApi({
      'GET /api/v1/me': () => ({ status: 200, body: viewerUser }),
      'GET /api/v1/orders': () => problem(400, 'validation.failed', 'Some fields are missing or not valid.', { sort: ['Sort by one of orderNo.'] }),
    });
    renderApp('/orders?sort=status');

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(screen.getByRole('form', { name: 'Order filters' })).toBeInTheDocument();
  });

  it('AC_15 a Viewer sees no order actions', async () => {
    stubOrderListApi(viewerUser, onePage);
    renderApp('/orders');

    await screen.findByRole('rowheader', { name: 'SO-5' });
    expect(screen.queryByRole('button', { name: 'New order' })).not.toBeInTheDocument();
  });

  it.each([plannerUser, adminUser])('AC_15 $role sees the order actions', async (currentUser) => {
    stubOrderListApi(currentUser, onePage);
    renderApp('/orders');

    expect(await screen.findByRole('button', { name: 'New order' })).toBeInTheDocument();
  });

  it('AC_45 AC_147 a Planner creates an order and is taken to it', async () => {
    const requests = stubOrderListApi(plannerUser, onePage, () => ({ status: 201, body: { id: order.id } }));
    const user = userEvent.setup();
    renderApp('/orders');

    await user.click(await screen.findByRole('button', { name: 'New order' }));
    const form = screen.getByRole('form', { name: 'New order' });
    await user.type(within(form).getByLabelText('Order number'), 'SO-5');
    await user.type(within(form).getByLabelText('Customer'), 'Tire Shop North');
    await user.selectOptions(await within(form).findByLabelText('Product'), 'PS-205 Passenger 205/55 R16');
    await user.type(within(form).getByLabelText('Quantity'), '101');
    await user.type(within(form).getByLabelText('Due date'), '2026-12-01');
    expect(within(form).getByLabelText('Priority')).toHaveValue('Normal');
    await user.selectOptions(within(form).getByLabelText('Priority'), 'High');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
    const sent = requests.find((request) => request.method === 'POST' && request.path === '/api/v1/orders')?.body as OrderRequest;
    expect(sent).toEqual({ orderNo: 'SO-5', customerName: 'Tire Shop North', productId: 3, quantity: 101, dueDate: '2026-12-01', priority: 'High' });
  });

  it('AC_26 AC_46 a rejected order shows each field error next to its field and keeps the entered values', async () => {
    stubOrderListApi(plannerUser, onePage, () =>
      problem(400, 'validation.failed', 'Some fields are missing or not valid.', { productId: ['This product has no routing yet, so no operations can be generated for it.'] }),
    );
    const user = userEvent.setup();
    renderApp('/orders');

    await user.click(await screen.findByRole('button', { name: 'New order' }));
    const form = screen.getByRole('form', { name: 'New order' });
    await user.type(within(form).getByLabelText('Order number'), 'SO-6');
    await user.selectOptions(await within(form).findByLabelText('Product'), 'TR-315 Truck 315/80 R22.5');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(within(form).getByLabelText('Product')).toHaveAccessibleDescription('This product has no routing yet, so no operations can be generated for it.');
    expect(within(form).getByLabelText('Order number')).toHaveValue('SO-6');
    expect(within(form).getByLabelText('Product')).toHaveValue('4');
  });
});
