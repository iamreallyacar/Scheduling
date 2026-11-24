import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { searchParamsOf } from '../api/request';
import { problemOf } from '../api/problem';
import type { OrderPageResponse } from '../api/generated';
import { useSignedInUser } from '../auth/signedInUser';
import { QueryResult } from '../forms/QueryResult';
import { OrderFilterForm } from './OrderFilterForm';
import { OrderForm } from './OrderForm';
import { narrowsTheList, orderListQueryFrom, type OrderListQuery } from './orderListQuery';
import { useOrderPage } from './orderQueries';
import { OrderStatusText } from './OrderStatusText';

// F-ORD-1 and F-ORD-4: the orders the server returns for the search, filters, sort and page in
// the address.
export function OrderListPage() {
  const { canPlan } = useSignedInUser();
  const [isCreating, setIsCreating] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();
  const query = orderListQueryFrom(searchParams);
  const orderPage = useOrderPage(query);
  const navigate = useNavigate();

  const showQuery = (nextQuery: OrderListQuery) => {
    setSearchParams(searchParamsOf(nextQuery));
  };

  return (
    <section aria-labelledby="orders-heading">
      <h1 id="orders-heading">Orders</h1>
      <OrderFilterForm
        key={searchParams.toString()}
        query={query}
        problem={orderPage.isError ? problemOf(orderPage.error) : null}
        onApply={showQuery}
      />
      <QueryResult query={orderPage}>
        {(page) =>
          page.items.length === 0 && page.page === 1 ? (
            <p className="page-message">{narrowsTheList(query) ? 'No orders match these filters.' : 'No orders yet.'}</p>
          ) : (
            <>
              {page.items.length === 0 ? <p className="page-message">Page {page.page} has no orders.</p> : <OrderTable page={page} />}
              <OrderPager
                page={page}
                onGoTo={(pageNo) => {
                  showQuery({ ...query, page: pageNo });
                }}
              />
            </>
          )
        }
      </QueryResult>
      {canPlan && !isCreating && (
        <button
          type="button"
          onClick={() => {
            setIsCreating(true);
          }}
        >
          New order
        </button>
      )}
      {isCreating && (
        <OrderForm
          order={null}
          onSaved={(createdOrderId) => {
            void navigate(`/orders/${String(createdOrderId)}`);
          }}
          onClose={() => {
            setIsCreating(false);
          }}
        />
      )}
    </section>
  );
}

function OrderTable({ page }: { page: OrderPageResponse }) {
  return (
    <table aria-label="Orders">
      <thead>
        <tr>
          <th scope="col">Order number</th>
          <th scope="col">Customer</th>
          <th scope="col">Product</th>
          <th scope="col">Quantity</th>
          <th scope="col">Due date</th>
          <th scope="col">Priority</th>
          <th scope="col">Status</th>
        </tr>
      </thead>
      <tbody>
        {page.items.map((order) => (
          <tr key={order.id}>
            <th scope="row">
              <Link to={`/orders/${String(order.id)}`}>{order.orderNo}</Link>
            </th>
            <td>{order.customerName}</td>
            <td>{order.product.code}</td>
            <td>{order.quantity}</td>
            <td>{order.dueDate}</td>
            <td>{order.priority}</td>
            <td>
              <OrderStatusText status={order.status} isLate={order.isLate} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function OrderPager({ page, onGoTo }: { page: OrderPageResponse; onGoTo: (pageNo: number) => void }) {
  const pageCount = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  return (
    <div className="row-actions">
      <button
        type="button"
        disabled={page.page <= 1}
        onClick={() => {
          onGoTo(page.page - 1);
        }}
      >
        Previous page
      </button>
      <p className="page-message">
        Page {page.page} of {pageCount}, {page.totalCount} orders
      </p>
      <button
        type="button"
        disabled={page.page >= pageCount}
        onClick={() => {
          onGoTo(page.page + 1);
        }}
      >
        Next page
      </button>
    </div>
  );
}
