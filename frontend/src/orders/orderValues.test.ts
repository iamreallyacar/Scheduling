import { describe, expect, it } from 'vitest';
import { priorityOfOption } from './orderValues';

describe('priorityOfOption', () => {
  it('AC_45 AC_47 gives back each priority the select offers', () => {
    expect(priorityOfOption('Low')).toBe('Low');
    expect(priorityOfOption('Normal')).toBe('Normal');
    expect(priorityOfOption('High')).toBe('High');
  });

  it('AC_45 AC_47 fails loudly on a value that is no priority instead of choosing Normal', () => {
    expect(() => priorityOfOption('Urgent')).toThrow(/Urgent/);
    expect(() => priorityOfOption('')).toThrow();
  });
});
