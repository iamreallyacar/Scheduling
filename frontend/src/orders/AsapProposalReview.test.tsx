import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, OrderProposalResponse, OrderResponse, TimelineResponse } from '../api/generated';
import { type FakeApiAnswer, type FakeApiRequest, plannerUser, problem, sentBody, stubApi, viewerUser } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';
import { order, passengerTire, summaryOf } from './orderSamples';

const PROPOSAL_PATH = '/api/v1/versions/live/orders/5/asap-proposal';
const COMMIT_PATH = `${PROPOSAL_PATH}/commit`;
const REVIEW_NAME = 'Schedule order SO-5 as soon as possible';

// Plant time is UTC+1 in November: the building lot runs 09:30 to 17:55, the curing lot 19:55 to 06:37.
const proposal: OrderProposalResponse = {
  orderId: 5,
  notBefore: '2026-11-16T08:30:00Z',
  lots: [
    {
      lotId: 21,
      lotNo: 1,
      sequenceNo: 1,
      resourceType: { id: 1, name: 'Building machine' },
      resource: { id: 6, name: 'Builder 2' },
      plannedStart: '2026-11-16T08:30:00Z',
      plannedEnd: '2026-11-16T16:55:00Z',
      isProposed: true,
    },
    {
      lotId: 22,
      lotNo: 1,
      sequenceNo: 2,
      resourceType: { id: 2, name: 'Curing press' },
      resource: { id: 7, name: 'Press 2' },
      plannedStart: '2026-11-16T18:55:00Z',
      plannedEnd: '2026-11-17T05:37:00Z',
      isProposed: true,
    },
  ],
  finishesAt: '2026-11-17T05:37:00Z',
  wouldBeLate: false,
};

function machine(id: number, name: string, placements: TimelineResponse['resourceTypes'][number]['resources'][number]['placements']) {
  return { id, name, isActive: true, status: 'Idle' as const, workingPeriods: [{ start: '2026-11-15T23:00:00Z', end: '2026-11-29T23:00:00Z' }], unavailabilities: [], placements };
}

// Builder 3 is not used by the order, so the preview leaves it out.
const timeline: TimelineResponse = {
  start: '2026-11-15T23:00:00Z',
  end: '2026-11-29T23:00:00Z',
  resourceTypes: [
    {
      id: 1,
      name: 'Building machine',
      resources: [
        machine(6, 'Builder 2', [
          {
            placementId: 40,
            plannedStart: '2026-11-16T06:00:00Z',
            plannedEnd: '2026-11-16T08:00:00Z',
            lotId: 90,
            lotNo: 1,
            quantity: 20,
            durationMinutes: 120,
            status: 'Scheduled',
            actions: { canChangePlacement: true, canRecordProgress: true },
            orderId: 9,
            orderNo: 'SO-9',
            product: { id: passengerTire.id, code: passengerTire.code, name: passengerTire.name },
            orderIsLate: false,
            sequenceNo: 1,
            resourceType: { id: 1, name: 'Building machine' },
          },
        ]),
        machine(8, 'Builder 3', []),
      ],
    },
    { id: 2, name: 'Curing press', resources: [machine(7, 'Press 2', [])] },
  ],
};

function stubProposalApi(
  currentUser: CurrentUserResponse,
  routes: Record<string, (request: FakeApiRequest) => FakeApiAnswer> = {},
  shownOrder: () => OrderResponse = () => order,
) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/orders/5': () => ({ status: 200, body: shownOrder() }),
    'GET /api/v1/orders': () => ({ status: 200, body: { items: [summaryOf(shownOrder())], page: 1, pageSize: 50, totalCount: 1 } }),
    [`POST ${PROPOSAL_PATH}`]: () => ({ status: 200, body: proposal }),
    'GET /api/v1/versions/live/timeline': () => ({ status: 200, body: timeline }),
    ...routes,
  });
}

async function openReview() {
  const user = userEvent.setup();
  renderApp('/orders/5');
  await user.click(await screen.findByRole('button', { name: 'Schedule as soon as possible' }));
  return { user, review: await screen.findByRole('region', { name: REVIEW_NAME }) };
}

function rowTexts(table: HTMLElement): (string | null)[] {
  return within(table)
    .getAllByRole('row')
    .map((row) => row.textContent);
}

describe('as-soon-as-possible review', () => {
  it('AC_201 AC_137 lists every lot, previews the machines the order uses and says whether the order would be late, saving nothing', async () => {
    const requests = stubProposalApi(plannerUser);
    const { review } = await openReview();

    const table = await within(review).findByRole('table', { name: 'Proposal' });
    expect(rowTexts(table)).toEqual([
      'SeqStageLotMachineStartEndNote',
      '1Building machine1Builder 216 Nov 2026, 09:3016 Nov 2026, 17:55Proposed',
      '2Curing press1Press 216 Nov 2026, 19:5517 Nov 2026, 06:37Proposed',
    ]);
    expect(within(review).getByText('Order would finish 17 Nov 2026, 06:37, due 2026-12-01: on time')).toBeInTheDocument();
    const preview = await within(review).findByRole('table', { name: 'Timeline' });
    expect(within(preview).getAllByRole('rowheader').map((header) => header.textContent)).toEqual(['Building machine', 'Builder 2', 'Curing press', 'Press 2']);
    expect(within(preview).getByText('SO-9/L1')).toBeInTheDocument();
    expect(within(preview).getAllByText('SO-5/L1 (proposed)')).toHaveLength(2);
    expect(within(review).getByRole('button', { name: 'Commit 2 placements' })).toBeInTheDocument();

    const proposalRequest = requests.find((request) => request.method === 'POST' && request.path === PROPOSAL_PATH);
    expect(proposalRequest?.query.has('notBefore')).toBe(false);
    const timelineRequest = requests.find((request) => request.path === '/api/v1/versions/live/timeline');
    expect([timelineRequest?.query.get('from'), timelineRequest?.query.get('to')]).toEqual(['2026-11-16', '2026-11-29']);
    expect(requests.some((request) => request.path === COMMIT_PATH)).toBe(false);
  });

  it('AC_228 Recalculate with a not-before time the page cannot read shows a field error and asks nothing', async () => {
    const requests = stubProposalApi(plannerUser, { [`POST ${PROPOSAL_PATH}`]: () => ({ status: 200, body: proposal }) });
    const { user, review } = await openReview();
    await within(review).findByRole('table', { name: 'Proposal' });

    await user.type(within(review).getByLabelText('Not before'), '20266-11-17T08:00');
    await user.click(within(review).getByRole('button', { name: 'Recalculate' }));

    expect(within(review).getByLabelText('Not before')).toHaveAccessibleDescription('Enter a date and time the page can read, with a four-digit year.');
    expect(within(review).getByRole('table', { name: 'Proposal' })).toBeInTheDocument();
    expect(requests.filter((request) => request.method === 'POST' && request.path === PROPOSAL_PATH)).toHaveLength(1);
  });

  it('AC_201 Recalculate asks again from the typed not-before time on the plant clock', async () => {
    const requests = stubProposalApi(plannerUser, {
      [`POST ${PROPOSAL_PATH}`]: (request) => ({ status: 200, body: request.query.has('notBefore') ? { ...proposal, wouldBeLate: true } : proposal }),
    });
    const { user, review } = await openReview();
    await within(review).findByRole('table', { name: 'Proposal' });

    await user.type(within(review).getByLabelText('Not before'), '2026-11-17T08:00');
    await user.click(within(review).getByRole('button', { name: 'Recalculate' }));

    expect(await within(review).findByText('Order would finish 17 Nov 2026, 06:37, due 2026-12-01: late')).toBeInTheDocument();
    const proposalRequests = requests.filter((request) => request.method === 'POST' && request.path === PROPOSAL_PATH);
    expect(proposalRequests.at(-1)?.query.get('notBefore')).toBe('2026-11-17T07:00:00.000Z');
  });

  it('AC_233 the not-before time of the review steps in 15 minutes', async () => {
    stubProposalApi(plannerUser);
    const { review } = await openReview();
    await within(review).findByRole('table', { name: 'Proposal' });

    expect(within(review).getByLabelText('Not before')).toHaveAttribute('step', '900');
  });

  it('AC_232 Commit is disabled with a hint while the typed time differs from the proposal, and the review follows Recalculate', async () => {
    const requests = stubProposalApi(plannerUser);
    const { user, review } = await openReview();
    await within(review).findByRole('table', { name: 'Proposal' });
    const field = within(review).getByLabelText('Not before');
    const commit = () => within(review).getByRole('button', { name: 'Commit 2 placements' });
    expect(commit()).toBeEnabled();

    await user.type(field, '2026-11-17T08:00');
    expect(commit()).toBeDisabled();
    expect(within(review).getByText('Recalculate to use the new time')).toBeInTheDocument();

    await user.clear(field);
    expect(commit()).toBeEnabled();
    expect(within(review).queryByText('Recalculate to use the new time')).not.toBeInTheDocument();

    await user.type(field, '2026-11-17T08:00');
    await user.click(within(review).getByRole('button', { name: 'Recalculate' }));
    await waitFor(() => { expect(commit()).toBeEnabled(); });
    expect(within(review).queryByText('Recalculate to use the new time')).not.toBeInTheDocument();
    const recalculated = requests.filter((request) => request.method === 'POST' && request.path === PROPOSAL_PATH).at(-1);
    expect(recalculated?.query.get('notBefore')).toMatch(/^2026-11-17T/);
  });

  it('AC_232 AC_228 a time the page cannot read keeps Commit disabled after Recalculate refuses it', async () => {
    stubProposalApi(plannerUser);
    const { user, review } = await openReview();
    await within(review).findByRole('table', { name: 'Proposal' });

    await user.type(within(review).getByLabelText('Not before'), '20266-11-17T08:00');
    await user.click(within(review).getByRole('button', { name: 'Recalculate' }));

    expect(within(review).getByRole('button', { name: 'Commit 2 placements' })).toBeDisabled();
    expect(within(review).getByText('Recalculate to use the new time')).toBeInTheDocument();
  });

  it('AC_202 the commit sends the proposal unchanged, closes the review and the page shows the placements', async () => {
    let isCommitted = false;
    const placedOrder: OrderResponse = {
      ...order,
      status: 'Scheduled',
      operations: order.operations.map((operation) => ({
        ...operation,
        lots: operation.lots.map((lot) => {
          const proposedLot = proposal.lots.find((candidate) => candidate.lotId === lot.id);
          return proposedLot === undefined
            ? lot
            : { ...lot, status: 'Scheduled' as const, placement: { resource: proposedLot.resource, plannedStart: proposedLot.plannedStart, plannedEnd: proposedLot.plannedEnd } };
        }),
      })),
    };
    const requests = stubProposalApi(
      plannerUser,
      {
        [`POST ${COMMIT_PATH}`]: () => {
          isCommitted = true;
          return { status: 200, body: [] };
        },
      },
      () => (isCommitted ? placedOrder : order),
    );
    const { user, review } = await openReview();

    await user.click(await within(review).findByRole('button', { name: 'Commit 2 placements' }));

    expect(await screen.findByText('Builder 2')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: REVIEW_NAME })).not.toBeInTheDocument();
    expect(sentBody(requests, 'POST', COMMIT_PATH)).toEqual({
      notBefore: '2026-11-16T08:30:00Z',
      placements: [
        { lotId: 21, resourceId: 6, plannedStart: '2026-11-16T08:30:00Z' },
        { lotId: 22, resourceId: 7, plannedStart: '2026-11-16T18:55:00Z' },
      ],
    });
  });

  it('AC_202 an empty proposal says every lot is placed and offers no commit', async () => {
    stubProposalApi(plannerUser, {
      [`POST ${PROPOSAL_PATH}`]: () => ({ status: 200, body: { ...proposal, lots: proposal.lots.map((lot) => ({ ...lot, isProposed: false })) } }),
    });
    const { review } = await openReview();

    expect(await within(review).findByText('Every lot of this order is already placed.')).toBeInTheDocument();
    expect(within(review).queryByRole('button', { name: /Commit/ })).not.toBeInTheDocument();
  });

  it('AC_202 a rejected proposal shows the detail and no table', async () => {
    const detail = 'Lot 1 of stage 2 (Curing press) has no free slot within 90 days after the search start, so nothing is proposed.';
    stubProposalApi(plannerUser, { [`POST ${PROPOSAL_PATH}`]: () => problem(422, 'slot.none', detail) });
    const { review } = await openReview();

    expect(await within(review).findByText(detail)).toBeInTheDocument();
    expect(within(review).queryByRole('table', { name: 'Proposal' })).not.toBeInTheDocument();
    expect(within(review).queryByRole('button', { name: /Commit/ })).not.toBeInTheDocument();
  });

  it('AC_202 a stale commit says that nothing was saved and keeps the review open', async () => {
    stubProposalApi(plannerUser, {
      [`POST ${COMMIT_PATH}`]: () => problem(409, 'proposal.stale', 'The schedule changed since this proposal was made, so nothing was saved.'),
    });
    const { user, review } = await openReview();

    await user.click(await within(review).findByRole('button', { name: 'Commit 2 placements' }));

    expect(await within(review).findByText('The schedule changed since this proposal was made. Nothing was saved.')).toBeInTheDocument();
    expect(within(review).getByRole('button', { name: 'Recalculate' })).toBeInTheDocument();
  });

  it('AC_201 the action is hidden from a Viewer and for a cancelled order', async () => {
    stubProposalApi(viewerUser);
    const { unmount } = renderApp('/orders/5');
    expect(await screen.findByRole('heading', { name: 'Order SO-5' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Schedule as soon as possible' })).not.toBeInTheDocument();
    unmount();

    stubProposalApi(plannerUser, {}, () => ({ ...order, status: 'Cancelled', cancelledAt: '2026-11-18T10:00:00Z', actions: { canScheduleAsap: false } }));
    renderApp('/orders/5');
    expect(await screen.findByRole('button', { name: 'Edit order' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Schedule as soon as possible' })).not.toBeInTheDocument();
  });
});
