import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { OrderSummaryResponse, ResourceViewResponse } from '../api/generated';
import { QueryResult } from '../forms/QueryResult';
import { OrderStatusText } from '../orders/OrderStatusText';
import { useDashboard } from './dashboardQueries';

// F-RPT-1: what needs the planner's attention, as the server selected and counted it (DEC-65).
// The page picks no order or machine itself.
export function DashboardPage() {
  const dashboard = useDashboard();
  return (
    <section aria-labelledby="dashboard-heading">
      <h1 id="dashboard-heading">Dashboard - Live</h1>
      <QueryResult query={dashboard}>
        {(answer) => (
          <>
            <DashboardPart heading={`Overdue orders (${String(answer.overdueOrders.length)})`}>
              <OrderTable label="Overdue orders" orders={answer.overdueOrders} emptyText="No overdue orders." />
            </DashboardPart>
            <DashboardPart heading={`Due by ${answer.dueSoonUntil} (${String(answer.dueSoonOrders.length)})`}>
              <OrderTable label={`Orders due by ${answer.dueSoonUntil}`} orders={answer.dueSoonOrders} emptyText={`No orders due by ${answer.dueSoonUntil}.`} />
            </DashboardPart>
            <DashboardPart heading="Schedule">
              <ul>
                <li>
                  <Link to="/schedule">{countText(answer.unscheduledLotCount, 'unscheduled lot', 'unscheduled lots')}</Link>
                </li>
                <li>
                  <Link to="/schedule">{countText(answer.conflictedPlacementCount, 'conflicted placement', 'conflicted placements')}</Link>
                </li>
              </ul>
            </DashboardPart>
            <DashboardPart heading={`Machines down (${String(answer.downMachines.length)})`}>
              <MachineTable machines={answer.downMachines} />
            </DashboardPart>
          </>
        )}
      </QueryResult>
    </section>
  );
}

function countText(count: number, singular: string, plural: string): string {
  return `${String(count)} ${count === 1 ? singular : plural}`;
}

function DashboardPart({ heading, children }: { heading: string; children: ReactNode }) {
  return (
    <section aria-label={heading}>
      <h2>{heading}</h2>
      {children}
    </section>
  );
}

function OrderTable({ label, orders, emptyText }: { label: string; orders: readonly OrderSummaryResponse[]; emptyText: string }) {
  if (orders.length === 0) {
    return <p className="page-message">{emptyText}</p>;
  }
  return (
    <table aria-label={label}>
      <thead>
        <tr>
          <th scope="col">Order number</th>
          <th scope="col">Customer</th>
          <th scope="col">Product</th>
          <th scope="col">Due date</th>
          <th scope="col">Status</th>
        </tr>
      </thead>
      <tbody>
        {orders.map((order) => (
          <tr key={order.id}>
            <th scope="row">
              <Link to={`/orders/${String(order.id)}`}>{order.orderNo}</Link>
            </th>
            <td>{order.customerName}</td>
            <td>{order.product.code}</td>
            <td>{order.dueDate}</td>
            <td>
              <OrderStatusText status={order.status} isLate={order.isLate} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function MachineTable({ machines }: { machines: readonly ResourceViewResponse[] }) {
  if (machines.length === 0) {
    return <p className="page-message">No machine is down.</p>;
  }
  return (
    <table aria-label="Machines down">
      <thead>
        <tr>
          <th scope="col">Name</th>
          <th scope="col">Type</th>
        </tr>
      </thead>
      <tbody>
        {machines.map((machine) => (
          <tr key={machine.id}>
            <th scope="row">
              <Link to={`/machines/${String(machine.id)}`}>{machine.name}</Link>
            </th>
            <td>{machine.resourceType.name}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
