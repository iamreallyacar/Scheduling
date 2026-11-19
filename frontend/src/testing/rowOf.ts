/** The table row holding the cell, so a test can look inside that row only. */
export function rowOf(cell: HTMLElement): HTMLElement {
  const row = cell.closest('tr');
  if (row === null) {
    throw new Error(`"${cell.textContent}" is not inside a table row.`);
  }
  return row;
}
