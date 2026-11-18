import { addDays, formatPlantDate, mondayOf, plantToday } from './timelineRange';

describe('timeline range', () => {
  it('AC_188 today is the plant-local date, not the browser date', () => {
    // Sun 2026-11-15 23:30 UTC is already Monday in Berlin.
    expect(plantToday('Europe/Berlin', new Date('2026-11-15T23:30:00Z'))).toBe('2026-11-16');
    expect(plantToday('America/New_York', new Date('2026-11-15T23:30:00Z'))).toBe('2026-11-15');
  });

  it('AC_188 the two weeks start on the Monday of the week', () => {
    expect(mondayOf('2026-11-22')).toBe('2026-11-16');
    expect(mondayOf('2026-11-16')).toBe('2026-11-16');
    expect(mondayOf('2026-11-18')).toBe('2026-11-16');
  });

  it('AC_188 days are counted on the calendar across month ends and DST changes', () => {
    expect(addDays('2026-10-24', 2)).toBe('2026-10-26');
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(formatPlantDate('2026-10-25')).toBe('Sun 25 Oct 2026');
  });
});
