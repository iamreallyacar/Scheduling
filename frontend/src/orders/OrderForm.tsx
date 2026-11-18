import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createOrder, updateOrder } from '../api/generated/endpoints.gen';
import type { OrderPriority, OrderRequest, OrderResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { NumberField, SelectField, TextField, type SelectOption } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { QueryResult } from '../forms/QueryResult';
import { useProducts } from '../products/productQueries';
import { ORDER_PRIORITIES } from './orderValues';

interface OrderFormProps {
  /** The order to change, or null to create one. */
  order: OrderResponse | null;
  /** Gets the id of the saved order, which the caller reads again (DEC-29). */
  onSaved: (savedOrderId: number) => void;
  onClose: () => void;
}

const NOTHING_CHOSEN = '';
const priorityOptions: SelectOption[] = ORDER_PRIORITIES.map((priority) => ({ value: priority, label: priority }));

// Create and change send the same fields; the server decides what may change (AC-47 to AC-49).
export function OrderForm({ order, onSaved, onClose }: OrderFormProps) {
  const products = useProducts();
  const [orderNo, setOrderNo] = useState(order?.orderNo ?? '');
  const [customerName, setCustomerName] = useState(order?.customerName ?? '');
  const [productId, setProductId] = useState(order === null ? NOTHING_CHOSEN : String(order.product.id));
  const [quantity, setQuantity] = useState(order === null ? '' : String(order.quantity));
  const [dueDate, setDueDate] = useState(order?.dueDate ?? '');
  const [priority, setPriority] = useState<OrderPriority>(order?.priority ?? 'Normal');

  const saveMutation = useMutation({
    mutationFn: async () => {
      const request: OrderRequest = {
        orderNo,
        customerName,
        productId: numberOrNull(productId),
        quantity: numberOrNull(quantity),
        dueDate: dueDate === '' ? null : dueDate,
        priority,
      };
      if (order === null) {
        return (await createOrder(request)).id;
      }
      await updateOrder(order.id, request);
      return order.id;
    },
    onSuccess: onSaved,
  });
  const problem = problemOf(saveMutation.error);
  const formName = order === null ? 'New order' : `Edit order ${order.orderNo}`;

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label={formName}>
      <h3>{formName}</h3>
      <ProblemSummary problem={problem} />
      <TextField label="Order number" name="orderNo" value={orderNo} onChange={setOrderNo} problem={problem} />
      <TextField label="Customer" name="customerName" value={customerName} onChange={setCustomerName} problem={problem} />
      <QueryResult query={products}>
        {(loadedProducts) => (
          <SelectField
            label="Product"
            name="productId"
            value={productId}
            onChange={setProductId}
            problem={problem}
            options={[
              { value: NOTHING_CHOSEN, label: 'Choose a product' },
              ...loadedProducts.map((product) => ({ value: String(product.id), label: `${product.code} ${product.name}` })),
            ]}
          />
        )}
      </QueryResult>
      <NumberField label="Quantity" name="quantity" min={1} value={quantity} onChange={setQuantity} problem={problem} />
      <TextField label="Due date" name="dueDate" type="date" value={dueDate} onChange={setDueDate} problem={problem} />
      <SelectField
        label="Priority"
        name="priority"
        value={priority}
        onChange={(chosen) => {
          setPriority(ORDER_PRIORITIES.find((candidate) => candidate === chosen) ?? 'Normal');
        }}
        problem={problem}
        options={priorityOptions}
      />
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save
        </button>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
    </form>
  );
}

function numberOrNull(text: string): number | null {
  return text.trim() === '' ? null : Number(text);
}
