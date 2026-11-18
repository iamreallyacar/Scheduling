import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse } from '../api/generated';
import { adminUser, notSignedIn, problem, stubApi } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

describe('sign in', () => {
  it('AC_6 shows the sign-in page without a session and returns to the requested page after sign-in', async () => {
    let signedInUser: CurrentUserResponse | null = null;
    const requests = stubApi({
      'GET /api/v1/me': () => (signedInUser === null ? notSignedIn() : { status: 200, body: signedInUser }),
      'POST /api/v1/auth/sign-in': () => {
        signedInUser = adminUser;
        return { status: 200, body: adminUser };
      },
      'GET /api/v1/products': () => ({ status: 200, body: [{ id: 1, code: 'PS-205', name: 'Passenger 205', spec: null }] }),
    });
    const user = userEvent.setup();
    renderApp('/products');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Products' })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText('Email'), 'ada@plant.test');
    await user.type(screen.getByLabelText('Password'), 'Secret-1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Products' })).toBeInTheDocument();
    expect(await screen.findByRole('rowheader', { name: 'PS-205' })).toBeInTheDocument();
    expect(screen.getByText('Ada Admin (Admin)')).toBeInTheDocument();
    expect(requests.find((request) => request.path === '/api/v1/auth/sign-in')?.body).toEqual({ email: 'ada@plant.test', password: 'Secret-1' });
  });

  it('AC_2 AC_26 shows the rejection and keeps the entered email', async () => {
    stubApi({
      'GET /api/v1/me': notSignedIn,
      'POST /api/v1/auth/sign-in': () => problem(401, 'auth.invalid_credentials', 'The email or password is not correct.'),
    });
    const user = userEvent.setup();
    renderApp('/');

    await user.type(await screen.findByLabelText('Email'), 'ada@plant.test');
    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is not correct.');
    expect(screen.getByLabelText('Email')).toHaveValue('ada@plant.test');
  });

  it('AC_5 signing out brings back the sign-in page', async () => {
    let signedInUser: CurrentUserResponse | null = adminUser;
    stubApi({
      'GET /api/v1/me': () => (signedInUser === null ? notSignedIn() : { status: 200, body: signedInUser }),
      'POST /api/v1/auth/sign-out': () => {
        signedInUser = null;
        return { status: 204 };
      },
      'GET /api/v1/products': () => ({ status: 200, body: [] }),
    });
    const user = userEvent.setup();
    renderApp('/products');

    await user.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('AC_6 shows the sign-in page when the session ends while the app is open', async () => {
    stubApi({
      'GET /api/v1/me': () => ({ status: 200, body: adminUser }),
      'GET /api/v1/products': notSignedIn,
    });
    renderApp('/products');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });
});
