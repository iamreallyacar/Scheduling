import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CalendarResponse, CalendarShiftsRequest, CurrentUserResponse, HolidayResponse } from '../api/generated';
import { adminUser, plannerUser, problem, stubApi, viewerUser, type FakeApiAnswer, type FakeApiRequest } from '../testing/fakeApi';
import { renderApp } from '../testing/renderApp';

const SHIFTS_PATH = '/api/v1/calendars/1/shifts';
const plantCalendar: CalendarResponse = {
  id: 1,
  name: 'Plant default',
  isPlantDefault: true,
  timeZone: 'Europe/Berlin',
  shifts: [
    { weekday: 'Monday', start: '00:00', end: '08:00' },
    { weekday: 'Monday', start: '08:00', end: '16:00' },
    { weekday: 'Friday', start: '16:00', end: '24:00' },
  ],
};
const newYear: HolidayResponse = { id: 7, date: '2027-01-01', name: 'New Year' };

interface CalendarApiOptions {
  holidays?: HolidayResponse[];
  saveShifts?: (request: FakeApiRequest) => FakeApiAnswer;
  extraRoutes?: Record<string, (request: FakeApiRequest) => FakeApiAnswer>;
}

function stubCalendarApi(currentUser: CurrentUserResponse, { holidays = [newYear], saveShifts, extraRoutes = {} }: CalendarApiOptions = {}) {
  return stubApi({
    'GET /api/v1/me': () => ({ status: 200, body: currentUser }),
    'GET /api/v1/calendars': () => ({ status: 200, body: [plantCalendar] }),
    'GET /api/v1/holidays': () => ({ status: 200, body: holidays }),
    ...(saveShifts === undefined ? {} : { [`PUT ${SHIFTS_PATH}`]: saveShifts }),
    ...extraRoutes,
  });
}

describe('plant calendar', () => {
  it.each([viewerUser, plannerUser])('AC_144 $role reads the time zone, shifts and holidays and sees no edit actions', async (currentUser) => {
    stubCalendarApi(currentUser);
    renderApp('/calendar');

    expect(await screen.findByText('Time zone: Europe/Berlin')).toBeInTheDocument();
    const shiftRows = within(screen.getByRole('table', { name: 'Shifts' })).getAllByRole('row').slice(1);
    expect(shiftRows.map((row) => row.textContent)).toEqual(['Monday00:0008:00', 'Monday08:0016:00', 'Friday16:0024:00']);
    expect(within(await screen.findByRole('table', { name: 'Holidays' })).getByText('New Year')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add|Remove|Save|Delete/ })).not.toBeInTheDocument();
  });

  it('AC_42 Admin removes and adds shifts and saves the whole week', async () => {
    const requests = stubCalendarApi(adminUser, { saveShifts: () => ({ status: 200, body: plantCalendar }) });
    const user = userEvent.setup();
    renderApp('/calendar');

    await user.click(await screen.findByRole('button', { name: 'Remove Shift 2' }));
    await user.click(screen.getByRole('button', { name: 'Add shift' }));
    const newShift = screen.getByRole('group', { name: 'Shift 3' });
    await user.selectOptions(within(newShift).getByLabelText('Weekday'), 'Saturday');
    await user.type(within(newShift).getByLabelText('Start (HH:mm)'), '06:00');
    await user.type(within(newShift).getByLabelText('End (HH:mm)'), '14:00');
    await user.click(screen.getByRole('button', { name: 'Save shifts' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Shifts saved.');
    const saved = requests.find((request) => request.method === 'PUT' && request.path === SHIFTS_PATH)?.body as CalendarShiftsRequest;
    expect(saved).toEqual({
      shifts: [
        { weekday: 'Monday', start: '00:00', end: '08:00' },
        { weekday: 'Friday', start: '16:00', end: '24:00' },
        { weekday: 'Saturday', start: '06:00', end: '14:00' },
      ],
    });
  });

  it('AC_26 a rejected shift save shows each field error next to its field and keeps the entered values', async () => {
    stubCalendarApi(adminUser, {
      saveShifts: () => problem(400, 'validation.failed', 'Some fields are missing or not valid.', { 'shifts[1].end': ['The shift must end after it starts.'] }),
    });
    const user = userEvent.setup();
    renderApp('/calendar');

    const secondShift = await screen.findByRole('group', { name: 'Shift 2' });
    await user.clear(within(secondShift).getByLabelText('End (HH:mm)'));
    await user.type(within(secondShift).getByLabelText('End (HH:mm)'), '07:00');
    await user.click(screen.getByRole('button', { name: 'Save shifts' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields are missing or not valid.');
    expect(within(secondShift).getByLabelText('End (HH:mm)')).toHaveAccessibleDescription('The shift must end after it starts.');
    expect(within(secondShift).getByLabelText('End (HH:mm)')).toHaveValue('07:00');
    expect(within(screen.getByRole('group', { name: 'Shift 1' })).getByLabelText('End (HH:mm)')).not.toBeInvalid();
  });

  it('AC_43 Admin adds a holiday and sees it in the list', async () => {
    const holidays: HolidayResponse[] = [newYear];
    const requests = stubCalendarApi(adminUser, {
      holidays,
      extraRoutes: {
        'POST /api/v1/holidays': (request) => {
          const created = { id: 8, ...(request.body as { date: string; name: string }) };
          holidays.push(created);
          return { status: 201, body: created };
        },
      },
    });
    const user = userEvent.setup();
    renderApp('/calendar');

    const form = await screen.findByRole('form', { name: 'Add holiday' });
    await user.type(within(form).getByLabelText('Date'), '2027-12-25');
    await user.type(within(form).getByLabelText('Name'), 'Christmas Day');
    await user.click(within(form).getByRole('button', { name: 'Add holiday' }));

    expect(await within(screen.getByRole('table', { name: 'Holidays' })).findByText('Christmas Day')).toBeInTheDocument();
    expect(requests.find((request) => request.method === 'POST' && request.path === '/api/v1/holidays')?.body).toEqual({ date: '2027-12-25', name: 'Christmas Day' });
    expect(within(form).getByLabelText('Name')).toHaveValue('');
  });

  it('AC_24 shows why a holiday was rejected and keeps the entered values', async () => {
    stubCalendarApi(adminUser, {
      extraRoutes: {
        'POST /api/v1/holidays': () => problem(409, 'conflict.duplicate', 'Another item already uses this date.', { date: ['Another item already uses this date.'] }),
      },
    });
    const user = userEvent.setup();
    renderApp('/calendar');

    const form = await screen.findByRole('form', { name: 'Add holiday' });
    await user.type(within(form).getByLabelText('Date'), '2027-01-01');
    await user.type(within(form).getByLabelText('Name'), 'Second New Year');
    await user.click(within(form).getByRole('button', { name: 'Add holiday' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('Another item already uses this date.');
    expect(within(form).getByLabelText('Date')).toHaveAccessibleDescription('Another item already uses this date.');
    expect(within(form).getByLabelText('Name')).toHaveValue('Second New Year');
  });

  it('AC_43 Admin deletes a holiday', async () => {
    const holidays: HolidayResponse[] = [newYear];
    const requests = stubCalendarApi(adminUser, {
      holidays,
      extraRoutes: {
        'DELETE /api/v1/holidays/7': () => {
          holidays.pop();
          return { status: 204 };
        },
      },
    });
    const user = userEvent.setup();
    renderApp('/calendar');

    await user.click(await screen.findByRole('button', { name: 'Delete New Year' }));

    expect(await screen.findByText('No holidays yet.')).toBeInTheDocument();
    expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/v1/holidays/7')).toBe(true);
  });
});
