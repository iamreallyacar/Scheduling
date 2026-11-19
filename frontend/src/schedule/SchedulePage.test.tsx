import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ConflictedPlacementResponse, ResourceViewResponse, UnscheduledLotResponse } from '../api/generated';
import { plantCalendar } from '../orders/orderSamples';
import { type FakeApiAnswer, type FakeApiRequest, plannerUser, sentBody, stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const UNSCHEDULED_LOTS_PATH = '/api/v1/versions/live/unscheduled-lots';
const CONFLICTS_PATH = '/api/v1/versions/live/conflicts';
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

// Listed later start first: the page must keep the order of the answer, not sort it.
const brokenDownPlacement: ConflictedPlacementResponse = {
  placementId: 90,
  plannedStart: '2026-11-17T05:00:00Z',
  plannedEnd: '2026-11-17T09:00:00Z',
  resource: { id: 6, name: 'Press 1' },
  lotId: 42,
  lotNo: 3,
  quantity: 200,
  durationMinutes: 240,
  orderId: 9,
  orderNo: 'SO-1042',
  sequenceNo: 2,
  resourceType: curing,
  violations: [
    { rule: 'R3', code: 'rule.R3.unavailable', conflict: { type: 'unavailability', id: 3 }, detail: 'The lot would overlap an unavailability of this machine.', earliestAllowedStart: null },
    { rule: 'R1', code: 'rule.R1.inactive', conflict: { type: 'resource', id: 6 }, detail: 'This machine is inactive.', earliestAllowedStart: null },
  ],
};
const outsideShiftPlacement: ConflictedPlacementResponse = {
  ...brokenDownPlacement,
  placementId: 91,
  plannedStart: '2026-11-16T05:00:00Z',
  plannedEnd: '2026-11-16T09:00:00Z',
  lotId: 43,
  lotNo: 1,
  orderId: 10,
  orderNo: 'SO-1047',
  violations: [{ rule: 'R6', code: 'rule.R6.outside_working_time', conflict: { type: 'resource', id: 6 }, detail: "The start is outside the machine's working time.", earliestAllowedStart: null }],
};

function stubScheduleApi(unscheduledLots: UnscheduledLotResponse[], conflictedPlacements: ConflictedPlacementResponse[] = []) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: viewerUser }),
    [`GET ${UNSCHEDULED_LOTS_PATH}`]: () => ({ status: 200, body: unscheduledLots }),
    [`GET ${CONFLICTS_PATH}`]: () => ({ status: 200, body: conflictedPlacements }),
    'GET /api/v1/calendars': () => ({ status: 200, body: [plantCalendar] }),
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

  it('AC_184 lists the conflicted placements in the order the API returns, on the plant clock, with every broken rule', async () => {
    stubScheduleApi([], [brokenDownPlacement, outsideShiftPlacement]);
    renderApp('/schedule');

    const conflicts = await screen.findByRole('table', { name: 'Conflicted placements (2)' });
    const rows = within(conflicts).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell').map((cell) => cell.textContent))).toEqual([
      [
        'SO-1042',
        '2 Curing press',
        '3',
        'Press 1',
        '17 Nov 2026, 06:00',
        '17 Nov 2026, 10:00',
        'The lot would overlap an unavailability of this machine. rule.R3.unavailableThis machine is inactive. rule.R1.inactive',
      ],
      ['SO-1047', '2 Curing press', '1', 'Press 1', '16 Nov 2026, 06:00', '16 Nov 2026, 10:00', "The start is outside the machine's working time. rule.R6.outside_working_time"],
    ]);
    expect(within(conflicts).getByRole('link', { name: 'SO-1042' })).toHaveAttribute('href', '/orders/9');
    expect(screen.getByText('Times in Europe/Berlin.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Move...' })).not.toBeInTheDocument();
  });

  it('AC_184 says that no placement breaks a rule when none is listed', async () => {
    stubScheduleApi([curingLot]);
    renderApp('/schedule');

    expect(await screen.findByText('No placement breaks a rule.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Conflicted placements (0)' })).toBeInTheDocument();
  });

  it('AC_184 a Planner moves a conflicted placement through the placement form filled in with it', async () => {
    stubApi({
      'GET /api/v1/me': () => ({ status: 200, body: plannerUser }),
      [`GET ${UNSCHEDULED_LOTS_PATH}`]: () => ({ status: 200, body: [] }),
      [`GET ${CONFLICTS_PATH}`]: () => ({ status: 200, body: [brokenDownPlacement] }),
      'GET /api/v1/calendars': () => ({ status: 200, body: [plantCalendar] }),
      'GET /api/v1/resources': () => ({ status: 200, body: [press1, press2] }),
    });
    const user = userEvent.setup();
    renderApp('/schedule');

    const conflicts = await screen.findByRole('table', { name: 'Conflicted placements (1)' });
    await user.click(within(conflicts).getByRole('button', { name: 'Move...' }));

    const form = await screen.findByRole('form', { name: 'Move lot - Order SO-1042, stage 2 Curing press, lot 3' });
    expect(await within(form).findByLabelText('Machine')).toHaveValue(String(press1.id));
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-17T06:00');
    expect(within(form).getByRole('button', { name: 'Remove placement' })).toBeInTheDocument();
  });

  it('AC_180 a Viewer sees no place action', async () => {
    stubScheduleApi([curingLot]);
    renderApp('/schedule');

    expect(await screen.findByRole('table', { name: 'Unscheduled lots (1)' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Place...' })).not.toBeInTheDocument();
  });

  it('AC_180 a Planner places a queued lot on an active machine of its type, typed on the plant clock and sent in UTC', async () => {
    let queue = [curingLot, buildingLot];
    const requests = stubPlannerApi(
      () => queue,
      () => {
        queue = [buildingLot];
        return { status: 200, body: { lotId: curingLot.id, resourceId: press2.id, plannedStart: '2026-11-16T07:00:00Z', plannedEnd: '2026-11-17T03:30:00Z' } };
      },
    );
    const user = userEvent.setup();
    renderApp('/schedule');

    const queueTable = await screen.findByRole('table', { name: 'Unscheduled lots (2)' });
    await user.click(within(within(queueTable).getByRole('row', { name: /SO-1063/ })).getByRole('button', { name: 'Place...' }));
    const form = await screen.findByRole('form', { name: 'Place lot - Order SO-1063, stage 2 Curing press, lot 1' });
    expect(within(form).getByText('Quantity 200, duration 1230 minutes of working time. Times in Europe/Berlin.')).toBeInTheDocument();
    const machine = await within(form).findByLabelText('Machine');
    expect(within(machine).getAllByRole('option').map((option) => option.textContent)).toEqual(['Choose a machine', 'Press 2']);
    expect(within(form).getByText('Planned end: Calculated when saved')).toBeInTheDocument();
    expect(within(form).queryByRole('button', { name: 'Remove placement' })).not.toBeInTheDocument();
    await user.selectOptions(machine, 'Press 2');
    await user.type(within(form).getByLabelText('Start'), '2026-11-16T08:00');
    await user.click(within(form).getByRole('button', { name: 'Save placement' }));

    expect(await screen.findByRole('table', { name: 'Unscheduled lots (1)' })).toBeInTheDocument();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    const machineRequest = requests.find((request) => request.path === '/api/v1/resources');
    expect([machineRequest?.query.get('resourceTypeId'), machineRequest?.query.get('isActive')]).toEqual(['2', 'true']);
    expect(sentBody(requests, 'PUT', `/api/v1/versions/live/lots/${String(curingLot.id)}/placement`)).toEqual({ resourceId: press2.id, plannedStart: '2026-11-16T07:00:00.000Z' });
  });

  it('AC_181 a rejected placement lists every broken rule with the earliest allowed start and keeps the entered values', async () => {
    stubPlannerApi(
      () => [curingLot],
      () => ({
        status: 422,
        body: {
          status: 422,
          code: 'rule.R2.overlap',
          detail: 'The lot would overlap another placement on this machine.',
          rule: 'R2',
          conflict: { type: 'placement', id: 40 },
          violations: [
            { rule: 'R2', code: 'rule.R2.overlap', conflict: { type: 'placement', id: 40 }, detail: 'The lot would overlap another placement on this machine.', earliestAllowedStart: null },
            {
              rule: 'R4',
              code: 'rule.R4.dependency',
              conflict: { type: 'lot', id: 51 },
              detail: 'The lot would start before a lot of an earlier stage ends, counting the lag.',
              earliestAllowedStart: '2026-11-16T09:00:00Z',
            },
          ],
        },
      }),
    );
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: 'Place...' }));
    const form = await screen.findByRole('form', { name: /^Place lot/ });
    await user.selectOptions(await within(form).findByLabelText('Machine'), 'Press 2');
    await user.type(within(form).getByLabelText('Start'), '2026-11-16T08:00');
    await user.click(within(form).getByRole('button', { name: 'Save placement' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('The lot would overlap another placement on this machine.');
    expect(within(within(form).getByRole('list', { name: 'Broken rules' })).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'The lot would overlap another placement on this machine. rule.R2.overlap',
      'The lot would start before a lot of an earlier stage ends, counting the lag. Earliest allowed start: 16 Nov 2026, 10:00. rule.R4.dependency',
    ]);
    expect(within(form).getByLabelText('Machine')).toHaveValue(String(press2.id));
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-16T08:00');
  });
});

const press1: ResourceViewResponse = { id: 6, name: 'Press 1', resourceType: curing, calendar: { id: 1, name: 'Plant default' }, isActive: true, status: 'Down' };
const press2: ResourceViewResponse = { id: 7, name: 'Press 2', resourceType: curing, calendar: { id: 1, name: 'Plant default' }, isActive: true, status: 'Idle' };

function stubPlannerApi(unscheduledLots: () => UnscheduledLotResponse[], placeLot: (request: FakeApiRequest) => FakeApiAnswer) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: plannerUser }),
    [`GET ${UNSCHEDULED_LOTS_PATH}`]: () => ({ status: 200, body: unscheduledLots() }),
    [`GET ${CONFLICTS_PATH}`]: () => ({ status: 200, body: [] }),
    'GET /api/v1/calendars': () => ({ status: 200, body: [plantCalendar] }),
    'GET /api/v1/resources': () => ({ status: 200, body: [press2] }),
    [`PUT /api/v1/versions/live/lots/${String(curingLot.id)}/placement`]: placeLot,
  });
}
