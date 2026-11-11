import { screen } from '@testing-library/react';
import { adminUser, stubApi } from './testing/fakeApi';
import { renderApp } from './testing/renderApp';

describe('App', () => {
  it('opens the machine register at the start address and offers the module areas', async () => {
    stubApi({
      'GET /api/v1/me': () => ({ status: 200, body: adminUser }),
      'GET /api/v1/resource-types': () => ({ status: 200, body: [] }),
      'GET /api/v1/resources': () => ({ status: 200, body: [] }),
    });
    renderApp('/');

    expect(await screen.findByRole('heading', { name: 'Machines' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Machines' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Products' })).toBeInTheDocument();
  });

  it('says so when a page does not exist', async () => {
    stubApi({ 'GET /api/v1/me': () => ({ status: 200, body: adminUser }) });
    renderApp('/no-such-page');

    expect(await screen.findByText('This page does not exist.')).toBeInTheDocument();
  });
});
