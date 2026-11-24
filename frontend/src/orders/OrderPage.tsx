import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { cancelOrder, deleteOrder } from '../api/generated/endpoints.gen';
import type { OrderResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { queryKeys } from '../api/queryKeys';
import { useSignedInUser } from '../auth/signedInUser';
import { PlantTime } from '../calendar/PlantTime';
import { ProblemSummary } from '../forms/ProblemSummary';
import { QueryResult } from '../forms/QueryResult';
import { PlacementForm } from '../schedule/PlacementForm';
import type { PlacementTarget } from '../schedule/placementTarget';
import { NotFoundPage } from '../shell/NotFoundPage';
import { AsapProposalReview } from './AsapProposalReview';
import { LotProgressForm, type ProgressTarget } from './LotProgressForm';
import { OrderForm } from './OrderForm';
import { useOrder } from './orderQueries';
import { OrderStatusText } from './OrderStatusText';
import { OrderTables } from './OrderTables';

// F-ORD-1 to F-ORD-3, F-ORD-5 and F-PRG-1: one order with its status, progress, operations and
// lots. Everyone reads; a Planner changes it and records the progress of its lots.
export function OrderPage() {
  const orderId = Number(useParams()['orderId']);
  return Number.isInteger(orderId) ? <OrderView orderId={orderId} /> : <NotFoundPage />;
}

function OrderView({ orderId }: { orderId: number }) {
  const order = useOrder(orderId);
  return (
    <section aria-labelledby="order-heading">
      <QueryResult query={order}>{(loadedOrder) => <LoadedOrder order={loadedOrder} />}</QueryResult>
      <p>
        <Link to="/orders">Back to orders</Link>
      </p>
    </section>
  );
}

function LoadedOrder({ order }: { order: OrderResponse }) {
  const { canPlan } = useSignedInUser();
  const [isEditing, setIsEditing] = useState(false);
  const [lotInForm, setLotInForm] = useState<PlacementTarget | null>(null);
  const [lotInProgressForm, setLotInProgressForm] = useState<ProgressTarget | null>(null);
  const [isReviewingProposal, setIsReviewingProposal] = useState(false);
  const navigate = useNavigate();

  // A write answers without the order; the query client reads it again (DEC-29, DEC-36).
  const cancelMutation = useMutation({ mutationFn: () => cancelOrder(order.id) });
  const deleteMutation = useMutation({
    mutationFn: () => deleteOrder(order.id),
    meta: { deletedQueryKey: queryKeys.order(order.id) },
    onSuccess: () => navigate('/orders'),
  });

  return (
    <>
      <h1 id="order-heading">Order {order.orderNo}</h1>
      <ProblemSummary problem={problemOf(cancelMutation.error) ?? problemOf(deleteMutation.error)} />
      <dl className="field-list">
        <dt>Status</dt>
        <dd>
          <OrderStatusText status={order.status} isLate={order.isLate} />
        </dd>
        <dt>Progress</dt>
        <dd>
          {order.progress.completedQuantity} of {order.progress.totalQuantity} tires completed
        </dd>
        <dt>Customer</dt>
        <dd>{order.customerName}</dd>
        <dt>Product</dt>
        <dd>
          {order.product.code} {order.product.name}
        </dd>
        <dt>Quantity</dt>
        <dd>{order.quantity}</dd>
        <dt>Due date</dt>
        <dd>{order.dueDate}</dd>
        <dt>Priority</dt>
        <dd>{order.priority}</dd>
        <dt>Created</dt>
        <dd>
          <PlantTime value={order.createdAt} />
        </dd>
        {order.cancelledAt !== null && (
          <>
            <dt>Cancelled</dt>
            <dd>
              <PlantTime value={order.cancelledAt} />
            </dd>
          </>
        )}
      </dl>
      {canPlan && !isEditing && (
        <div className="form-actions">
          <button
            type="button"
            onClick={() => {
              setIsEditing(true);
            }}
          >
            Edit order
          </button>
          <button
            type="button"
            disabled={cancelMutation.isPending}
            onClick={() => {
              deleteMutation.reset();
              cancelMutation.mutate();
            }}
          >
            Cancel order
          </button>
          <button
            type="button"
            disabled={deleteMutation.isPending}
            onClick={() => {
              cancelMutation.reset();
              deleteMutation.mutate();
            }}
          >
            Delete order
          </button>
          {order.status !== 'Cancelled' && (
            <button
              type="button"
              onClick={() => {
                setIsReviewingProposal(true);
              }}
            >
              Schedule as soon as possible
            </button>
          )}
        </div>
      )}
      {isReviewingProposal && (
        <AsapProposalReview
          order={order}
          onClose={() => {
            setIsReviewingProposal(false);
          }}
        />
      )}
      {isEditing && (
        <OrderForm
          order={order}
          onSaved={() => {
            setIsEditing(false);
          }}
          onClose={() => {
            setIsEditing(false);
          }}
        />
      )}
      <OrderTables order={order} lotActions={canPlan ? { openPlacement: setLotInForm, openProgress: setLotInProgressForm } : null} />
      {lotInForm !== null && (
        <PlacementForm
          // A new key gives the form fresh state when another lot is chosen.
          key={lotInForm.lotId}
          target={lotInForm}
          onClose={() => {
            setLotInForm(null);
          }}
        />
      )}
      {lotInProgressForm !== null && (
        <LotProgressForm
          key={lotInProgressForm.lotId}
          target={lotInProgressForm}
          onClose={() => {
            setLotInProgressForm(null);
          }}
        />
      )}
    </>
  );
}
