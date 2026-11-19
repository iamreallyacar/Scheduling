import type { NamedReference } from '../api/generated';

interface TitledLot {
  orderNo: string;
  sequenceNo: number;
  resourceType: NamedReference;
  lotNo: number;
}

/** "Order SO-5, stage 2 Curing press, lot 1", as the placement form and the timeline popover name a lot. */
export function lotTitle(lot: TitledLot): string {
  return `Order ${lot.orderNo}, stage ${String(lot.sequenceNo)} ${lot.resourceType.name}, lot ${String(lot.lotNo)}`;
}
