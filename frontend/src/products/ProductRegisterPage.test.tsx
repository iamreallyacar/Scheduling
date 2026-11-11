import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, ProductResponse } from '../api/generated';
import { adminUser, plannerUser, problem, stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const passengerTire: ProductResponse = { id: 1, code: 'PS-205', name: 'Passenger 205/55 R16', spec: '91V' };

function stubProductApi(currentUser: CurrentUserResponse, products: ProductResponse[]) {
  return {
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/products': () => ({ status: 200, body: products }),
  };
}

describe('product register', () => {
  it('lists products with code, name and spec', async () => {
    stubApi(stubProductApi(viewerUser, [passengerTire]));
    renderApp('/products');

    const row = (await screen.findByRole('rowheader', { name: 'PS-205' })).closest('tr');
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText('Passenger 205/55 R16')).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText('91V')).toBeInTheDocument();
  });

  it.each([viewerUser, plannerUser])('AC_15 $role sees no product edit actions', async (currentUser) => {
    stubApi(stubProductApi(currentUser, [passengerTire]));
    renderApp('/products');

    await screen.findByRole('rowheader', { name: 'PS-205' });
    expect(screen.queryByRole('button', { name: /Add|Edit|Delete/ })).not.toBeInTheDocument();
  });

  it('AC_15 Admin sees the product edit actions', async () => {
    stubApi(stubProductApi(adminUser, [passengerTire]));
    renderApp('/products');

    expect(await screen.findByRole('button', { name: 'Edit PS-205' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete PS-205' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add product' })).toBeInTheDocument();
  });

  it('AC_26 a rejected form shows the detail, each field error next to its field, and keeps the entered values', async () => {
    stubApi({
      ...stubProductApi(adminUser, []),
      'POST /api/v1/products': () =>
        problem(400, 'validation.failed', 'Some fields are missing or not valid.', {
          code: ['The Code field is required.'],
          name: ['The Name field is required.'],
        }),
    });
    const user = userEvent.setup();
    renderApp('/products');

    await user.click(await screen.findByRole('button', { name: 'Add product' }));
    await user.type(screen.getByLabelText('Size or spec (optional)'), '205/55 R16');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(screen.getByLabelText('Code')).toHaveAccessibleDescription('The Code field is required.');
    expect(screen.getByLabelText('Name')).toHaveAccessibleDescription('The Name field is required.');
    expect(screen.getByLabelText('Size or spec (optional)')).not.toBeInvalid();
    expect(screen.getByLabelText('Size or spec (optional)')).toHaveValue('205/55 R16');
  });

  it('saves a new product and shows it in the list', async () => {
    const products: ProductResponse[] = [];
    const requests = stubApi({
      ...stubProductApi(adminUser, products),
      'POST /api/v1/products': (request) => {
        const created = { id: 2, spec: null, ...(request.body as { code: string; name: string }) };
        products.push(created);
        return { status: 201, body: created };
      },
    });
    const user = userEvent.setup();
    renderApp('/products');

    await user.click(await screen.findByRole('button', { name: 'Add product' }));
    await user.type(screen.getByLabelText('Code'), 'TR-315');
    await user.type(screen.getByLabelText('Name'), 'Truck 315/80');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('rowheader', { name: 'TR-315' })).toBeInTheDocument();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(requests.find((request) => request.method === 'POST' && request.path === '/api/v1/products')?.body).toEqual({ code: 'TR-315', name: 'Truck 315/80', spec: '' });
  });

  it('AC_20 shows why a product could not be deleted', async () => {
    stubApi({
      ...stubProductApi(adminUser, [passengerTire]),
      'DELETE /api/v1/products/1': () => problem(409, 'conflict.in_use', "Product 'PS-205' cannot be deleted because order 'SO-1' uses it."),
    });
    const user = userEvent.setup();
    renderApp('/products');

    await user.click(await screen.findByRole('button', { name: 'Delete PS-205' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("order 'SO-1' uses it");
    expect(screen.getByRole('rowheader', { name: 'PS-205' })).toBeInTheDocument();
  });
});
