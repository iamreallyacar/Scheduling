import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ConflictedPlacementResponse, ResourceViewResponse, TimelinePlacementResponse, TimelineResponse } from '../api/generated';
import { plannerUser, problem, stubApi, viewerUser, type FakeApiAnswer, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const TIMELINE_PATH = '/api/v1/versions/live/timeline';
const building = { id: 1, name: 'Building machine' };
const curing = { id: 2, name: 'Curing press' };
const product = { id: 3, code: 'P-205/55R16', name: 'Passenger 205' };

// Mon 16 Nov to Sun 29 Nov 2026 in Berlin (UTC+1): 336 hours from 2026-11-15T23:00Z.
const curingBar: TimelinePlacementResponse = {
  placementId: 90,
  plannedStart: '2026-11-17T05:00:00Z',
  plannedEnd: '2026-11-17T09:00:00Z',
  lotId: 42,
  lotNo: 3,
  quantity: 200,
  durationMinutes: 240,
  status: 'Scheduled',
  orderId: 9,
  orderNo: 'SO-1042',
  product,
  orderIsLate: true,
  sequenceNo: 2,
  resourceType: curing,
};
const buildingBar: TimelinePlacementResponse = {
  ...curingBar,
  placementId: 92,
  plannedStart: '2026-11-16T07:00:00Z',
  plannedEnd: '2026-11-16T09:00:00Z',
  lotId: 51,
  lotNo: 2,
  durationMinutes: 120,
  sequenceNo: 1,
  resourceType: building,
};

// Listed building first: the page keeps the order of the answer.
const twoWeeks: TimelineResponse = {
  start: '2026-11-15T23:00:00Z',
  end: '2026-11-29T23:00:00Z',
  resourceTypes: [
    {
      ...building,
      resources: [{ id: 5, name: 'Builder 1', isActive: true, status: 'Idle', workingPeriods: [], unavailabilities: [], placements: [buildingBar] }],
    },
    {
      ...curing,
      resources: [
        {
          id: 6,
          name: 'Press 1',
          isActive: true,
          status: 'Down',
          workingPeriods: [
            { start: '2026-11-15T23:00:00Z', end: '2026-11-20T23:00:00Z' },
            { start: '2026-11-22T23:00:00Z', end: '2026-11-27T23:00:00Z' },
          ],
          unavailabilities: [
            { id: 3, resourceId: 6, kind: 'Breakdown', start: '2026-11-17T08:00:00Z', end: null, reason: 'Bladder leak' },
            { id: 4, resourceId: 6, kind: 'Planned', start: '2026-11-24T08:00:00Z', end: '2026-11-24T12:00:00Z', reason: null },
          ],
          placements: [curingBar],
        },
      ],
    },
  ],
};

const conflictedCuring: ConflictedPlacementResponse = {
  placementId: 90,
  plannedStart: curingBar.plannedStart,
  plannedEnd: curingBar.plannedEnd,
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
    {
      rule: 'R4',
      code: 'rule.R4.dependency',
      conflict: { type: 'lot', id: 51 },
      detail: 'The lot would start before a lot of an earlier stage ends, counting the lag.',
      earliestAllowedStart: '2026-11-17T06:00:00Z',
    },
    { rule: 'R2', code: 'rule.R2.overlap', conflict: { type: 'placement', id: 99 }, detail: 'The lot would overlap another placement on this machine.', earliestAllowedStart: null },
  ],
};
// After the shown range, so the timeline holds none of it.
const conflictedLater: ConflictedPlacementResponse = {
  ...conflictedCuring,
  placementId: 93,
  plannedStart: '2026-12-03T05:00:00Z',
  plannedEnd: '2026-12-03T09:00:00Z',
  lotId: 70,
  orderId: 12,
  orderNo: 'SO-1070',
  violations: [{ rule: 'R1', code: 'rule.R1.inactive', conflict: { type: 'resource', id: 6 }, detail: 'This machine is inactive.', earliestAllowedStart: null }],
};

const press1: ResourceViewResponse = { id: 6, name: 'Press 1', resourceType: curing, calendar: { id: 1, name: 'Plant default' }, isActive: true, status: 'Down' };

interface ScheduleStub {
  user?: typeof plannerUser;
  timeline?: (request: FakeApiRequest) => TimelineResponse;
  placeLot?: (request: FakeApiRequest) => FakeApiAnswer;
  conflicts?: () => FakeApiAnswer | Promise<FakeApiAnswer>;
}

const bothConflicts = (): FakeApiAnswer => ({ status: 200, body: [conflictedCuring, conflictedLater] });

function stubScheduleApi({ user = viewerUser, timeline = () => twoWeeks, placeLot, conflicts = bothConflicts }: ScheduleStub = {}) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: user }),
    [`GET ${TIMELINE_PATH}`]: (request) => ({ status: 200, body: timeline(request) }),
    'GET /api/v1/versions/live/conflicts': conflicts,
    'GET /api/v1/versions/live/unscheduled-lots': () => ({ status: 200, body: [] }),
    'GET /api/v1/resources': () => ({ status: 200, body: [press1] }),
    ...(placeLot === undefined ? {} : { [`PUT /api/v1/versions/live/lots/${String(curingBar.lotId)}/placement`]: placeLot }),
  });
}

function timelineRanges(requests: FakeApiRequest[]): string[] {
  return requests.filter((request) => request.path === TIMELINE_PATH).map((request) => `${request.query.get('from') ?? ''} ${request.query.get('to') ?? ''}`);
}

function machineRow(name: string): HTMLElement {
  return screen.getByRole('row', { name: new RegExp(`^${name}`) });
}

// Left edge and width in percent of the shown range.
function positionOf(element: HTMLElement): [number, number] {
  return [parseFloat(element.style.left), parseFloat(element.style.width)];
}

describe('timeline', () => {
  beforeEach(() => {
    // Wed 18 Nov 2026 11:00 in Berlin.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-18T10:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('AC_188 shows the two weeks from the Monday of the current plant week and moves by the shown length', async () => {
    const requests = stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    expect(await screen.findByRole('table', { name: 'Timeline' })).toBeInTheDocument();
    expect(screen.getByText('Mon 16 Nov 2026 - Sun 29 Nov 2026')).toBeInTheDocument();
    expect(screen.getByText('Times in Europe/Berlin')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next >' }));
    expect(await screen.findByText('Mon 30 Nov 2026 - Sun 13 Dec 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Day' }));
    expect(await screen.findByText('Mon 30 Nov 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: '< Prev' }));
    expect(await screen.findByText('Sun 29 Nov 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Today' }));
    expect(await screen.findByText('Wed 18 Nov 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Two weeks' }));
    await user.click(screen.getByRole('button', { name: 'Today' }));
    expect(await screen.findByText('Mon 16 Nov 2026 - Sun 29 Nov 2026')).toBeInTheDocument();

    expect([...new Set(timelineRanges(requests))]).toEqual([
      '2026-11-16 2026-11-29',
      '2026-11-30 2026-12-13',
      '2026-11-30 2026-11-30',
      '2026-11-29 2026-11-29',
      '2026-11-18 2026-11-18',
      '2026-11-18 2026-12-01',
    ]);
  });

  it('AC_188 groups the machines by resource type in the order of the answer with their status, shows one type, and hides the queue', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    const timeline = await screen.findByRole('table', { name: 'Timeline' });
    expect(within(timeline).getAllByRole('rowheader').map((header) => header.textContent)).toEqual(['Building machine', 'Builder 1', 'Curing press', 'Press 1']);
    expect(within(machineRow('Builder 1')).getByRole('cell', { name: 'Idle' })).toBeInTheDocument();
    expect(within(machineRow('Press 1')).getByRole('cell', { name: 'Down' })).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Resource type'), 'Curing press');
    expect(within(timeline).getAllByRole('rowheader').map((header) => header.textContent)).toEqual(['Curing press', 'Press 1']);

    expect(screen.getByRole('heading', { name: 'Unscheduled lots (0)' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Hide queue' }));
    expect(screen.queryByRole('heading', { name: 'Unscheduled lots (0)' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Show queue' }));
    expect(screen.getByRole('heading', { name: 'Unscheduled lots (0)' })).toBeInTheDocument();
  });

  it('AC_189 draws bars, unavailabilities, non-working time and now where the API puts them, and marks the conflicted placements', async () => {
    stubScheduleApi();
    renderApp('/schedule');

    await screen.findByRole('table', { name: 'Timeline' });
    const press1Row = machineRow('Press 1');
    // 30 hours of 336 from the start, 4 hours long.
    expect(positionOf(within(press1Row).getByRole('button', { name: '! SO-1042/L3' }))).toEqual([8.929, 1.19]);
    expect(positionOf(within(machineRow('Builder 1')).getByRole('button', { name: 'SO-1042/L2' }))).toEqual([2.381, 0.595]);
    expect(positionOf(within(press1Row).getByText('Breakdown, no end'))).toEqual([9.821, 90.179]);
    expect(positionOf(within(press1Row).getByText('Planned maintenance'))).toEqual([59.821, 1.19]);
    expect(within(press1Row).getAllByTitle('Non-working time').map(positionOf)).toEqual([
      [35.714, 14.286],
      [85.714, 14.286],
    ]);
    expect(within(machineRow('Builder 1')).getAllByTitle('Non-working time').map(positionOf)).toEqual([[0, 100]]);
    expect(parseFloat(screen.getByRole('separator', { name: 'Now' }).style.left)).toBe(17.56);
  });

  it('AC_189 the conflicted-placements button shows how many the API lists and shows only their bars', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    const onlyConflicted = await screen.findByRole('button', { name: '! 2 conflicted placements' });
    expect(await screen.findByRole('button', { name: 'SO-1042/L2' })).toBeInTheDocument();
    await user.click(onlyConflicted);

    expect(onlyConflicted).toHaveAttribute('aria-pressed', 'true');
    expect(screen.queryByRole('button', { name: 'SO-1042/L2' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '! SO-1042/L3' })).toBeInTheDocument();
  });

  it('AC_190 a bar opens a popover with the lot as the API returns it, and a Viewer sees no move action', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! SO-1042/L3' }));

    const popover = screen.getByRole('dialog', { name: 'Order SO-1042, stage 2 Curing press, lot 3' });
    expect(within(popover).getByText('P-205/55R16 Passenger 205')).toBeInTheDocument();
    expect(within(popover).getByText('200')).toBeInTheDocument();
    expect(within(popover).getByText('Press 1')).toBeInTheDocument();
    expect(within(popover).getByText('17 Nov 2026, 06:00 - 17 Nov 2026, 10:00')).toBeInTheDocument();
    expect(within(popover).getByText('Scheduled, order late')).toBeInTheDocument();
    expect(within(within(popover).getByRole('list', { name: 'Broken rules' })).getAllByRole('listitem')).toHaveLength(3);
    expect(within(popover).getByRole('link', { name: 'Open order' })).toHaveAttribute('href', '/orders/9');
    expect(within(popover).queryByRole('button', { name: 'Move...' })).not.toBeInTheDocument();

    await user.click(within(popover).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('AC_190 the popover closes when a filter hides its bar', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! SO-1042/L3' }));
    await user.selectOptions(screen.getByLabelText('Resource type'), 'Building machine');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('AC_190 a Planner moves the lot of a bar through the placement form filled in with its placement', async () => {
    stubScheduleApi({ user: plannerUser });
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! SO-1042/L3' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Move...' }));

    const form = await screen.findByRole('form', { name: 'Move lot - Order SO-1042, stage 2 Curing press, lot 3' });
    expect(await within(form).findByLabelText('Machine')).toHaveValue(String(press1.id));
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-17T06:00');
    expect(within(form).getByRole('button', { name: 'Remove placement' })).toBeInTheDocument();
  });

  it('AC_191 a violation links to its item when the timeline holds it, and a lot or placement link opens that bar', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! SO-1042/L3' }));
    const popover = screen.getByRole('dialog');
    const rules = within(within(popover).getByRole('list', { name: 'Broken rules' })).getAllByRole('listitem');
    expect(rules.map((rule) => within(rule).queryByRole('link')?.textContent ?? null)).toEqual(['Show unavailability', null, null]);
    expect(rules.map((rule) => within(rule).queryByRole('button')?.textContent ?? null)).toEqual([null, 'Show lot', null]);
    expect(within(popover).getByRole('link', { name: 'Show unavailability' })).toHaveAttribute('href', '/machines/6');

    await user.click(within(popover).getByRole('button', { name: 'Show lot' }));
    expect(screen.getByRole('dialog', { name: 'Order SO-1042, stage 1 Building machine, lot 2' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'SO-1042/L2' })).toHaveAttribute('aria-pressed', 'true');

    const laterConflict = within(screen.getByRole('table', { name: 'Conflicted placements (2)' })).getByRole('row', { name: /SO-1070/ });
    expect(within(laterConflict).getByRole('link', { name: 'Show machine' })).toHaveAttribute('href', '/machines/6');
  });

  it('AC_191 a link to a bar the filters hide clears the filters and opens that bar', async () => {
    stubScheduleApi();
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! 2 conflicted placements' }));
    await user.click(screen.getByRole('button', { name: '! SO-1042/L3' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Show lot' }));

    expect(screen.getByRole('dialog', { name: 'Order SO-1042, stage 1 Building machine, lot 2' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '! 2 conflicted placements' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('AC_191 a rejected save in the placement form links to the placement it overlaps', async () => {
    stubScheduleApi({
      user: plannerUser,
      placeLot: () => ({
        status: 422,
        body: {
          status: 422,
          code: 'rule.R2.overlap',
          detail: 'The lot would overlap another placement on this machine.',
          rule: 'R2',
          conflict: { type: 'placement', id: 92 },
          violations: [{ rule: 'R2', code: 'rule.R2.overlap', conflict: { type: 'placement', id: 92 }, detail: 'The lot would overlap another placement on this machine.', earliestAllowedStart: null }],
        },
      }),
    });
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: '! SO-1042/L3' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Move...' }));
    const form = await screen.findByRole('form', { name: /^Move lot/ });
    await within(form).findByLabelText('Machine');
    await user.click(within(form).getByRole('button', { name: 'Save placement' }));
    await user.click(await within(form).findByRole('button', { name: 'Show placement' }));

    expect(screen.getByRole('dialog', { name: 'Order SO-1042, stage 1 Building machine, lot 2' })).toBeInTheDocument();
  });

  it('AC_192 while the conflict list loads, no bar is marked, the page says so, and the markers follow once it has loaded', async () => {
    let answerConflicts: (answer: FakeApiAnswer) => void = () => {
      throw new Error('The conflict list was not requested.');
    };
    stubScheduleApi({
      conflicts: () =>
        new Promise((resolve) => {
          answerConflicts = resolve;
        }),
    });
    const user = userEvent.setup();
    renderApp('/schedule');

    await user.click(await screen.findByRole('button', { name: 'SO-1042/L3' }));
    expect(screen.getByRole('status')).toHaveTextContent('Loading the conflict markers…');
    expect(screen.queryByRole('button', { name: /conflicted placements/ })).not.toBeInTheDocument();
    const popover = screen.getByRole('dialog', { name: 'Order SO-1042, stage 2 Curing press, lot 3' });
    expect(within(popover).getByText('The broken rules are not known until the conflict list has loaded.')).toBeInTheDocument();

    answerConflicts(bothConflicts());
    expect(await screen.findByRole('button', { name: '! SO-1042/L3' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '! 2 conflicted placements' })).toBeInTheDocument();
    const loadedPopover = screen.getByRole('dialog', { name: 'Order SO-1042, stage 2 Curing press, lot 3' });
    expect(within(within(loadedPopover).getByRole('list', { name: 'Broken rules' })).getAllByRole('listitem')).toHaveLength(3);
    expect(within(loadedPopover).queryByText('The broken rules are not known until the conflict list has loaded.')).not.toBeInTheDocument();
    expect(screen.queryByText('Loading the conflict markers…')).not.toBeInTheDocument();
  });

  it('AC_192 after the conflict list failed, the timeline is shown without markers and the page says so', async () => {
    stubScheduleApi({ conflicts: () => problem(500, 'server.error', 'The server failed.') });
    const user = userEvent.setup();
    renderApp('/schedule');

    expect(await screen.findByText('The conflict markers could not be loaded, so no bar is marked as conflicted.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /conflicted placements/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'SO-1042/L3' }));
    const popover = screen.getByRole('dialog', { name: 'Order SO-1042, stage 2 Curing press, lot 3' });
    expect(within(popover).getByText('The broken rules are not known until the conflict list has loaded.')).toBeInTheDocument();
    expect(within(popover).queryByRole('list', { name: 'Broken rules' })).not.toBeInTheDocument();
  });
});
