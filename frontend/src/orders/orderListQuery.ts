import type { ListOrdersData } from '../api/generated';

export type OrderListQuery = NonNullable<ListOrdersData['query']>;

const FILTER_PARAMETERS: readonly (keyof OrderListQuery)[] = ['search', 'status', 'late', 'customer', 'dueFrom', 'dueTo', 'priority'];

/**
 * The order list keeps its query in the address under the API's parameter names, so a filtered
 * list can be bookmarked and is still there after opening an order and going back. Values are
 * passed on as they are; the server checks them (AC-149).
 */
export function orderListQueryFrom(params: URLSearchParams): OrderListQuery {
  const query: OrderListQuery = {};
  const pageValue = numberParam(params, 'page');
  if (pageValue !== undefined) {
    query.page = pageValue;
  }
  const pageSizeValue = numberParam(params, 'pageSize');
  if (pageSizeValue !== undefined) {
    query.pageSize = pageSizeValue;
  }
  const searchValue = params.get('search') ?? undefined;
  if (searchValue !== undefined) {
    query.search = searchValue;
  }
  const statusValue = listParam(params, 'status');
  if (statusValue !== undefined) {
    query.status = statusValue;
  }
  const lateValue = flagParam(params, 'late');
  if (lateValue !== undefined) {
    query.late = lateValue;
  }
  const customerValue = params.get('customer') ?? undefined;
  if (customerValue !== undefined) {
    query.customer = customerValue;
  }
  const dueFromValue = params.get('dueFrom') ?? undefined;
  if (dueFromValue !== undefined) {
    query.dueFrom = dueFromValue;
  }
  const dueToValue = params.get('dueTo') ?? undefined;
  if (dueToValue !== undefined) {
    query.dueTo = dueToValue;
  }
  const priorityValue = listParam(params, 'priority');
  if (priorityValue !== undefined) {
    query.priority = priorityValue;
  }
  const sortValue = params.get('sort') ?? undefined;
  if (sortValue !== undefined) {
    query.sort = sortValue;
  }
  return query;
}

function numberParam(params: URLSearchParams, name: string): number | undefined {
  const value = params.get(name);
  return value === null || value === '' ? undefined : Number(value);
}

function listParam(params: URLSearchParams, name: string): string[] | undefined {
  const values = params.getAll(name);
  return values.length > 0 ? values : undefined;
}

function flagParam(params: URLSearchParams, name: string): boolean | undefined {
  const value = params.get(name);
  return value === 'true' ? true : value === 'false' ? false : undefined;
}

/** True when the query narrows the list, as opposed to only paging or sorting it. */
export function narrowsTheList(query: OrderListQuery): boolean {
  return FILTER_PARAMETERS.some((name) => query[name] !== undefined);
}
