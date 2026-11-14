import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, ProductResponse, ResourceTypeResponse, RoutingRequest, RoutingResponse } from '../api/generated';
import { adminUser, plannerUser, problem, stubApi, viewerUser, type FakeApiAnswer, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const ROUTING_PATH = '/api/v1/products/1/routing';
const passengerTire: ProductResponse = { id: 1, code: 'PS-205', name: 'Passenger 205/55 R16', spec: null };
const building: ResourceTypeResponse = { id: 1, name: 'Building machine', kind: 'Machine' };
const curing: ResourceTypeResponse = { id: 2, name: 'Curing press', kind: 'Machine' };
const inspection: ResourceTypeResponse = { id: 3, name: 'Inspection', kind: 'Machine' };

const threeStageRouting: RoutingResponse = {
  stages: [
    { sequenceNo: 1, resourceType: building, setupMinutes: 0, cycleMinutes: 5, unitsPerCycle: 1 },
    { sequenceNo: 2, resourceType: curing, setupMinutes: 30, cycleMinutes: 12, unitsPerCycle: 4 },
    { sequenceNo: 3, resourceType: inspection, setupMinutes: 0, cycleMinutes: 2, unitsPerCycle: 1 },
  ],
  dependencies: [
    { predecessorSequenceNo: 1, successorSequenceNo: 2, minLagMinutes: 0 },
    { predecessorSequenceNo: 2, successorSequenceNo: 3, minLagMinutes: 45 },
  ],
};
const noRouting: RoutingResponse = { stages: [], dependencies: [] };

function stubRoutingApi(currentUser: CurrentUserResponse, routing: RoutingResponse, saveRouting?: (request: FakeApiRequest) => FakeApiAnswer) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/products/1': () => ({ status: 200, body: passengerTire }),
    'GET /api/v1/resource-types': () => ({ status: 200, body: [building, curing, inspection] }),
    [`GET ${ROUTING_PATH}`]: () => ({ status: 200, body: routing }),
    ...(saveRouting === undefined ? {} : { [`PUT ${ROUTING_PATH}`]: saveRouting }),
  });
}

function savedRequest(requests: FakeApiRequest[]): RoutingRequest {
  return requests.find((request) => request.method === 'PUT' && request.path === ROUTING_PATH)?.body as RoutingRequest;
}

describe('routing of a product', () => {
  it.each([viewerUser, plannerUser])('AC_15 $role reads the routing and sees no edit actions', async (currentUser) => {
    stubRoutingApi(currentUser, threeStageRouting);
    renderApp('/products/1/routing');

    expect(await screen.findByRole('heading', { name: 'Routing of PS-205' })).toBeInTheDocument();
    const curingRow = (await screen.findByRole('rowheader', { name: 'Stage 2' })).closest('tr');
    expect(curingRow).toHaveTextContent('Curing press');
    expect(curingRow).toHaveTextContent('30');
    expect(within(screen.getByRole('table', { name: 'Dependencies' })).getByText('45')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add|Remove|Save/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
  });

  it('AC_38 adding a stage adds a dependency from the stage before it with lag 0, which can be edited', async () => {
    const requests = stubRoutingApi(adminUser, noRouting, () => ({ status: 200, body: threeStageRouting }));
    const user = userEvent.setup();
    renderApp('/products/1/routing');

    await user.click(await screen.findByRole('button', { name: 'Add stage' }));
    expect(screen.queryByRole('group', { name: 'Dependency 1' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Add stage' }));

    const dependency = within(screen.getByRole('group', { name: 'Dependency 1' }));
    expect(dependency.getByLabelText('Predecessor stage')).toHaveDisplayValue('Stage 1');
    expect(dependency.getByLabelText('Successor stage')).toHaveDisplayValue('Stage 2');
    expect(dependency.getByLabelText('Minimum lag (minutes)')).toHaveValue(0);

    await user.clear(dependency.getByLabelText('Minimum lag (minutes)'));
    await user.type(dependency.getByLabelText('Minimum lag (minutes)'), '20');
    const firstStage = within(screen.getByRole('group', { name: 'Stage 1' }));
    await user.selectOptions(firstStage.getByLabelText('Machine type'), 'Building machine');
    await user.type(firstStage.getByLabelText('Cycle minutes'), '5');
    const secondStage = within(screen.getByRole('group', { name: 'Stage 2' }));
    await user.selectOptions(secondStage.getByLabelText('Machine type'), 'Curing press');
    await user.type(secondStage.getByLabelText('Cycle minutes'), '12');
    await user.type(secondStage.getByLabelText('Units per cycle (optional)'), '4');
    await user.click(screen.getByRole('button', { name: 'Save routing' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Routing saved.');
    expect(savedRequest(requests)).toEqual({
      stages: [
        { sequenceNo: 1, resourceTypeId: 1, setupMinutes: null, cycleMinutes: 5, unitsPerCycle: null },
        { sequenceNo: 2, resourceTypeId: 2, setupMinutes: null, cycleMinutes: 12, unitsPerCycle: 4 },
      ],
      dependencies: [{ predecessorSequenceNo: 1, successorSequenceNo: 2, minLagMinutes: 20 }],
    });
    // The editor now shows what the server answered.
    expect(screen.getByRole('group', { name: 'Stage 3' })).toBeInTheDocument();
  });

  it('AC_38 the added dependency can be removed and another added, so parallel branches are possible', async () => {
    const requests = stubRoutingApi(adminUser, threeStageRouting, () => ({ status: 200, body: threeStageRouting }));
    const user = userEvent.setup();
    renderApp('/products/1/routing');

    await user.click(await screen.findByRole('button', { name: 'Remove Dependency 1' }));
    await user.click(screen.getByRole('button', { name: 'Add dependency' }));
    const addedDependency = within(screen.getByRole('group', { name: 'Dependency 2' }));
    await user.selectOptions(addedDependency.getByLabelText('Predecessor stage'), 'Stage 1');
    await user.selectOptions(addedDependency.getByLabelText('Successor stage'), 'Stage 3');
    await user.click(screen.getByRole('button', { name: 'Save routing' }));

    await screen.findByRole('status');
    expect(savedRequest(requests).dependencies).toEqual([
      { predecessorSequenceNo: 2, successorSequenceNo: 3, minLagMinutes: 45 },
      { predecessorSequenceNo: 1, successorSequenceNo: 3, minLagMinutes: 0 },
    ]);
  });

  it('AC_143 removing a stage moves the later stages up and removes its dependencies', async () => {
    const requests = stubRoutingApi(adminUser, threeStageRouting, () => ({ status: 200, body: noRouting }));
    const user = userEvent.setup();
    renderApp('/products/1/routing');

    await user.click(await screen.findByRole('button', { name: 'Remove Stage 1' }));

    expect(screen.queryByRole('group', { name: 'Stage 3' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Stage 1' })).getByLabelText('Machine type')).toHaveDisplayValue('Curing press');
    expect(screen.queryByRole('group', { name: 'Dependency 2' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Save routing' }));
    await screen.findByRole('status');
    expect(savedRequest(requests)).toEqual({
      stages: [
        { sequenceNo: 1, resourceTypeId: 2, setupMinutes: 30, cycleMinutes: 12, unitsPerCycle: 4 },
        { sequenceNo: 2, resourceTypeId: 3, setupMinutes: 0, cycleMinutes: 2, unitsPerCycle: 1 },
      ],
      dependencies: [{ predecessorSequenceNo: 1, successorSequenceNo: 2, minLagMinutes: 45 }],
    });
  });

  it('AC_26 a rejected routing shows the detail, each field error next to its stage field, and keeps the entered values', async () => {
    stubRoutingApi(adminUser, threeStageRouting, () =>
      problem(400, 'validation.failed', 'Some fields are missing or not valid.', {
        'stages[1].cycleMinutes': ['The field CycleMinutes must be between 1 and 2147483647.'],
      }),
    );
    const user = userEvent.setup();
    renderApp('/products/1/routing');

    const secondStage = within(await screen.findByRole('group', { name: 'Stage 2' }));
    await user.clear(secondStage.getByLabelText('Cycle minutes'));
    await user.type(secondStage.getByLabelText('Cycle minutes'), '0');
    await user.click(screen.getByRole('button', { name: 'Save routing' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(secondStage.getByLabelText('Cycle minutes')).toHaveAccessibleDescription('The field CycleMinutes must be between 1 and 2147483647.');
    expect(secondStage.getByLabelText('Cycle minutes')).toHaveValue(0);
    expect(within(screen.getByRole('group', { name: 'Stage 1' })).getByLabelText('Cycle minutes')).not.toBeInvalid();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('AC_36 shows which stages make a dependency invalid', async () => {
    stubRoutingApi(adminUser, threeStageRouting, () => problem(400, 'routing.invalid_dependency', 'The dependencies form a cycle through stages 1, 2.'));
    const user = userEvent.setup();
    renderApp('/products/1/routing');

    await user.click(await screen.findByRole('button', { name: 'Save routing' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('cycle through stages 1, 2');
  });
});
