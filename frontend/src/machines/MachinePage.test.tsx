import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CurrentUserResponse, ResourceViewResponse, UnavailabilityResponse } from '../api/generated';
import { plannerUser, problem, stubApi, viewerUser, type FakeApiAnswer, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const press: ResourceViewResponse = {
  id: 20,
  name: 'Press 1',
  resourceType: { id: 2, name: 'Curing press' },
  calendar: { id: 1, name: 'Plant default' },
  isActive: true,
  status: 'Down',
};
const calendar = { id: 1, name: 'Plant default', isPlantDefault: true, timeZone: 'Europe/Berlin', shifts: [] };
const openBreakdown: UnavailabilityResponse = { id: 7, resourceId: 20, kind: 'Breakdown', start: '2026-11-18T08:00:00Z', end: null, reason: 'Hydraulic leak' };
const maintenance: UnavailabilityResponse = { id: 6, resourceId: 20, kind: 'Planned', start: '2026-11-17T05:00:00Z', end: '2026-11-17T09:00:00Z', reason: null };

const UNAVAILABILITIES = '/api/v1/resources/20/unavailabilities';

function stubMachinePageApi(currentUser: CurrentUserResponse, writes: Record<string, (request: FakeApiRequest) => FakeApiAnswer> = {}) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/resources/20': () => ({ status: 200, body: press }),
    'GET /api/v1/calendars': () => ({ status: 200, body: [calendar] }),
    [`GET ${UNAVAILABILITIES}`]: () => ({ status: 200, body: [openBreakdown, maintenance] }),
    ...writes,
  });
}

describe('machine page', () => {
  it('AC_173 AC_171 shows the machine with the status the server returns and its unavailabilities on the plant clock', async () => {
    stubMachinePageApi(viewerUser);
    renderApp('/machines/20');

    expect(await screen.findByRole('heading', { name: 'Press 1' })).toBeInTheDocument();
    expect(screen.getByText('Down')).toBeInTheDocument();
    expect(screen.getByText('Times in Europe/Berlin')).toBeInTheDocument();
    const rows = within(await screen.findByRole('table', { name: 'Unavailabilities' })).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringMatching(/Breakdown.*18 Nov 2026, 09:00.*no end.*Hydraulic leak/),
      expect.stringMatching(/Planned maintenance.*17 Nov 2026, 06:00.*17 Nov 2026, 10:00/),
    ]);
  });

  it('AC_171 a viewer sees the list without the actions', async () => {
    stubMachinePageApi(viewerUser);
    renderApp('/machines/20');

    await screen.findByRole('table', { name: 'Unavailabilities' });
    expect(screen.queryByRole('button', { name: /Record|Edit|Delete/ })).not.toBeInTheDocument();
  });

  it('AC_171 a planner records an open breakdown typed on the plant clock and sent in UTC', async () => {
    const requests = stubMachinePageApi(plannerUser, {
      [`POST ${UNAVAILABILITIES}`]: (request) => ({ status: 201, body: { ...openBreakdown, id: 8, ...(request.body as object) } }),
    });
    const user = userEvent.setup();
    renderApp('/machines/20');

    await user.click(await screen.findByRole('button', { name: 'Record unavailability' }));
    const form = screen.getByRole('form', { name: 'Record unavailability' });
    await user.selectOptions(within(form).getByLabelText('Kind'), 'Breakdown');
    await user.type(within(form).getByLabelText('Start'), '2026-11-19T07:30');
    await user.type(within(form).getByLabelText('Reason'), 'Bladder burst');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('button', { name: 'Record unavailability' })).toBeInTheDocument();
    expect(requests.find((request) => request.method === 'POST')?.body).toEqual({
      kind: 'Breakdown',
      start: '2026-11-19T06:30:00.000Z',
      end: null,
      reason: 'Bladder burst',
    });
  });

  it('AC_171 a planner gives an open breakdown its end, starting from the saved values', async () => {
    const requests = stubMachinePageApi(plannerUser, {
      [`PUT ${UNAVAILABILITIES}/7`]: (request) => ({ status: 200, body: { ...openBreakdown, ...(request.body as object) } }),
    });
    const user = userEvent.setup();
    renderApp('/machines/20');

    await user.click(await screen.findByRole('button', { name: 'Edit breakdown from 18 Nov 2026, 09:00' }));
    const form = screen.getByRole('form', { name: 'Edit unavailability' });
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-18T09:00');
    await user.type(within(form).getByLabelText('End'), '2026-11-18T13:15');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('button', { name: 'Record unavailability' })).toBeInTheDocument();
    expect(requests.find((request) => request.method === 'PUT')?.body).toEqual({
      kind: 'Breakdown',
      start: '2026-11-18T08:00:00.000Z',
      end: '2026-11-18T12:15:00.000Z',
      reason: 'Hydraulic leak',
    });
  });

  it('AC_171 a rejected save shows the problem next to its field and keeps the entered values', async () => {
    stubMachinePageApi(plannerUser, {
      [`POST ${UNAVAILABILITIES}`]: () =>
        problem(400, 'validation.failed', 'Some fields are missing or not valid.', {
          end: ['Planned maintenance needs an end; only a breakdown can have no known end.'],
        }),
    });
    const user = userEvent.setup();
    renderApp('/machines/20');

    await user.click(await screen.findByRole('button', { name: 'Record unavailability' }));
    const form = screen.getByRole('form', { name: 'Record unavailability' });
    await user.selectOptions(within(form).getByLabelText('Kind'), 'Planned maintenance');
    await user.type(within(form).getByLabelText('Start'), '2026-11-19T07:30');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await within(form).findByText('Planned maintenance needs an end; only a breakdown can have no known end.')).toBeInTheDocument();
    expect(within(form).getByLabelText('End')).toHaveAttribute('aria-invalid', 'true');
    expect(within(form).getByLabelText('Start')).toHaveValue('2026-11-19T07:30');
  });

  it('AC_171 a planner deletes an unavailability', async () => {
    const requests = stubMachinePageApi(plannerUser, {
      [`DELETE ${UNAVAILABILITIES}/6`]: () => ({ status: 204 }),
    });
    const user = userEvent.setup();
    renderApp('/machines/20');

    await user.click(await screen.findByRole('button', { name: 'Delete planned maintenance from 17 Nov 2026, 06:00' }));

    await vi.waitFor(() => {
      expect(requests.some((request) => request.method === 'DELETE' && request.path === `${UNAVAILABILITIES}/6`)).toBe(true);
    });
  });
});
