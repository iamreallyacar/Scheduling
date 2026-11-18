import { MutationObserver, QueryObserver, type QueryClient, type QueryKey } from '@tanstack/react-query';
import { createAppQueryClient } from './queryClient';
import { queryKeys } from './queryKeys';

interface CountedQuery {
  queryKey: QueryKey;
  readCount: () => number;
}

// Loads the query once, and keeps it on screen when `isOnScreen`, as a mounted page would.
async function loadQuery(queryClient: QueryClient, queryKey: QueryKey, isOnScreen: boolean): Promise<CountedQuery> {
  let reads = 0;
  const queryFn = () => {
    reads += 1;
    return Promise.resolve(`answer ${String(reads)}`);
  };
  await queryClient.query({ queryKey, queryFn });
  if (isOnScreen) {
    new QueryObserver(queryClient, { queryKey, queryFn, refetchOnMount: false }).subscribe(() => undefined);
  }
  return { queryKey, readCount: () => reads };
}

describe('the cache refresh after a write', () => {
  it('AC_164 reads every query on screen again before the follow-up runs and marks the others stale', async () => {
    const queryClient = createAppQueryClient();
    const currentUser = await loadQuery(queryClient, queryKeys.currentUser, true);
    const products = await loadQuery(queryClient, queryKeys.products, true);
    const orderPage = await loadQuery(queryClient, queryKeys.orderPage({ page: 1 }), false);
    let productReadsAtFollowUp = 0;

    await new MutationObserver(queryClient, {
      mutationFn: () => Promise.resolve('saved'),
      onSuccess: () => {
        productReadsAtFollowUp = products.readCount();
      },
    }).mutate();

    expect(productReadsAtFollowUp).toBe(2);
    expect(orderPage.readCount()).toBe(1);
    expect(queryClient.getQueryState(orderPage.queryKey)?.isInvalidated).toBe(true);
    expect(currentUser.readCount()).toBe(1);
    expect(queryClient.getQueryState(currentUser.queryKey)?.isInvalidated).toBe(false);
  });

  it('AC_164 drops the query of a deleted row instead of reading it again', async () => {
    const queryClient = createAppQueryClient();
    const deletedOrder = await loadQuery(queryClient, queryKeys.order(7), true);
    const products = await loadQuery(queryClient, queryKeys.products, true);

    await new MutationObserver(queryClient, {
      mutationFn: () => Promise.resolve(),
      meta: { deletedQueryKey: deletedOrder.queryKey },
    }).mutate();

    expect(deletedOrder.readCount()).toBe(1);
    expect(queryClient.getQueryState(deletedOrder.queryKey)).toBeUndefined();
    expect(products.readCount()).toBe(2);
  });

  it('AC_164 reads nothing again for a write that skips the refresh, as signing out does', async () => {
    const queryClient = createAppQueryClient();
    const products = await loadQuery(queryClient, queryKeys.products, true);

    await new MutationObserver(queryClient, {
      mutationFn: () => Promise.resolve(),
      meta: { skipsQueryRefresh: true },
    }).mutate();

    expect(products.readCount()).toBe(1);
    expect(queryClient.getQueryState(products.queryKey)?.isInvalidated).toBe(false);
  });

  it('AC_164 reads nothing again when the write fails', async () => {
    const queryClient = createAppQueryClient();
    const products = await loadQuery(queryClient, queryKeys.products, true);

    await expect(
      new MutationObserver(queryClient, { mutationFn: () => Promise.reject(new Error('rejected')) }).mutate(),
    ).rejects.toThrow('rejected');

    expect(products.readCount()).toBe(1);
    expect(queryClient.getQueryState(products.queryKey)?.isInvalidated).toBe(false);
  });
});
