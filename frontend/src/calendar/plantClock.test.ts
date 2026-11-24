import { isPlantLocalText, plantLocalToUtc, utcToPlantLocal } from './plantClock';

const BERLIN = 'Europe/Berlin';

describe('plant clock', () => {
  it('AC_171 reads a typed plant-local time as the UTC instant on the plant wall clock', () => {
    expect(plantLocalToUtc('2026-11-17T06:00', BERLIN)).toBe('2026-11-17T05:00:00.000Z');
    expect(plantLocalToUtc('2026-07-10T06:00', BERLIN)).toBe('2026-07-10T04:00:00.000Z');
  });

  it('AC_171 reads a local time that occurs twice as its first occurrence (A38)', () => {
    // Clocks go back from 03:00 to 02:00 on 2026-10-25 in Berlin.
    expect(plantLocalToUtc('2026-10-25T02:30', BERLIN)).toBe('2026-10-25T00:30:00.000Z');
  });

  it('AC_171 reads a local time inside a DST gap as the first time after the gap (A38)', () => {
    // Clocks go from 02:00 to 03:00 on 2027-03-28 in Berlin.
    expect(plantLocalToUtc('2027-03-28T02:30', BERLIN)).toBe('2027-03-28T01:00:00.000Z');
  });

  it('AC_171 shows a UTC time as the plant-local value a date and time field holds', () => {
    expect(utcToPlantLocal('2026-11-17T05:00:00Z', BERLIN)).toBe('2026-11-17T06:00');
    expect(utcToPlantLocal('2026-10-25T01:30:00Z', BERLIN)).toBe('2026-10-25T02:30');
  });

  it('AC_228 rejects a text that matches the pattern but names no real time', () => {
    for (const impossible of ['2026-13-01T08:00', '2026-02-31T08:00', '2026-11-17T25:00', '2026-11-17T08:60', '0026-11-17T08:00']) {
      expect(isPlantLocalText(impossible)).toBe(false);
    }
    expect(isPlantLocalText('2026-02-28T23:59')).toBe(true);
  });

  it('AC_228 reads a local time inside a DST gap without failing', () => {
    expect(isPlantLocalText('2027-03-28T02:30')).toBe(true);
  });
});
