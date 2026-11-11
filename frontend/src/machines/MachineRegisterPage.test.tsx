import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, ResourceResponse, ResourceTypeResponse } from '../api/generated';
import { adminUser, plannerUser, problem, stubApi, viewerUser, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const building: ResourceTypeResponse = { id: 1, name: 'Building machine', kind: 'Machine' };
const curing: ResourceTypeResponse = { id: 2, name: 'Curing press', kind: 'Machine' };
const plantCalendar = { id: 1, name: 'Plant default' };
const builder: ResourceResponse = { id: 10, name: 'Builder 1', resourceType: building, calendar: plantCalendar, isActive: true };
const press: ResourceResponse = { id: 20, name: 'Press 1', resourceType: curing, calendar: plantCalendar, isActive: false };

function stubMachineApi(currentUser: CurrentUserResponse) {
  return {
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/resource-types': () => ({ status: 200, body: [building, curing] }),
    // Like the server: the filter in the query decides what is listed.
    'GET /api/v1/resources': (request: FakeApiRequest) => ({
      status: 200,
      body: [builder, press].filter((machine) => {
        const typeId = request.query.get('resourceTypeId');
        return typeId === null || machine.resourceType.id === Number(typeId);
      }),
    }),
  };
}

describe('machine register', () => {
  it('AC_30 lists machines with type, calendar and active flag', async () => {
    stubApi(stubMachineApi(viewerUser));
    renderApp('/machines');

    const pressRow = (await screen.findByRole('rowheader', { name: 'Press 1' })).closest('tr');
    expect(pressRow).not.toBeNull();
    expect(within(pressRow as HTMLElement).getByText('Curing press')).toBeInTheDocument();
    expect(within(pressRow as HTMLElement).getByText('Plant default')).toBeInTheDocument();
    expect(within(pressRow as HTMLElement).getByText('No')).toBeInTheDocument();
    expect(screen.getByRole('rowheader', { name: 'Builder 1' })).toBeInTheDocument();
  });

  it('AC_30 passes the type and active filters to the server', async () => {
    const requests = stubApi(stubMachineApi(viewerUser));
    const user = userEvent.setup();
    renderApp('/machines');
    await screen.findByRole('rowheader', { name: 'Builder 1' });

    await user.selectOptions(screen.getByLabelText('Type'), 'Curing press');
    await user.selectOptions(screen.getByLabelText('Show'), 'Inactive only');

    expect(await screen.findByRole('rowheader', { name: 'Press 1' })).toBeInTheDocument();
    expect(screen.queryByRole('rowheader', { name: 'Builder 1' })).not.toBeInTheDocument();
    expect(requests.filter((request) => request.path === '/api/v1/resources').at(-1)?.query.toString()).toBe('resourceTypeId=2&isActive=false');
  });

  it.each([viewerUser, plannerUser])('AC_15 $role sees no machine edit actions', async (currentUser) => {
    stubApi(stubMachineApi(currentUser));
    renderApp('/machines');

    await screen.findByRole('rowheader', { name: 'Press 1' });
    expect(screen.queryByRole('button', { name: /Add|Edit|Delete|Rename/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Actions' })).not.toBeInTheDocument();
  });

  it('AC_15 Admin sees the machine and machine type edit actions', async () => {
    stubApi(stubMachineApi(adminUser));
    renderApp('/machines');

    expect(await screen.findByRole('button', { name: 'Edit Press 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete Press 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add machine' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rename Curing press' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add machine type' })).toBeInTheDocument();
  });

  it('AC_29 sends the changed name, type and active flag of a machine', async () => {
    const requests = stubApi({
      ...stubMachineApi(adminUser),
      'PUT /api/v1/resources/20': () => ({ status: 200, body: press }),
    });
    const user = userEvent.setup();
    renderApp('/machines');

    await user.click(await screen.findByRole('button', { name: 'Edit Press 1' }));
    const form = screen.getByRole('form', { name: 'Edit machine Press 1' });
    await user.clear(within(form).getByLabelText('Name'));
    await user.type(within(form).getByLabelText('Name'), 'Press A');
    await user.selectOptions(within(form).getByLabelText('Machine type'), 'Building machine');
    await user.click(within(form).getByLabelText('Active'));
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    await screen.findByRole('button', { name: 'Add machine' });
    expect(requests.find((request) => request.method === 'PUT')?.body).toEqual({ name: 'Press A', resourceTypeId: 1, isActive: true });
  });

  it('AC_26 a rejected machine form shows the field errors and keeps the entered name', async () => {
    stubApi({
      ...stubMachineApi(adminUser),
      'POST /api/v1/resources': () =>
        problem(400, 'validation.failed', 'Some fields are missing or not valid.', { resourceTypeId: ['The ResourceTypeId field is required.'] }),
    });
    const user = userEvent.setup();
    renderApp('/machines');

    await user.click(await screen.findByRole('button', { name: 'Add machine' }));
    const form = screen.getByRole('form', { name: 'Add machine' });
    await user.type(within(form).getByLabelText('Name'), 'Press 9');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(within(form).getByLabelText('Machine type')).toHaveAccessibleDescription('The ResourceTypeId field is required.');
    expect(within(form).getByLabelText('Name')).toHaveValue('Press 9');
  });

  it('AC_20 shows why a machine type in use could not be deleted', async () => {
    stubApi({
      ...stubMachineApi(adminUser),
      'DELETE /api/v1/resource-types/2': () =>
        problem(409, 'conflict.in_use', "Machine type 'Curing press' cannot be deleted because machine 'Press 1' has this type."),
    });
    const user = userEvent.setup();
    renderApp('/machines');

    await user.click(await screen.findByRole('button', { name: 'Delete Curing press' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("machine 'Press 1' has this type");
  });
});
