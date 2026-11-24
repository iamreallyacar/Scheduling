import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createOrder, updateOrder } from '../api/generated/endpoints.gen';
import type { OrderPriority, OrderRequest, OrderResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { NumberField, SelectField, TextField, type SelectOption } from '../forms/Fields';
import { NOTHING_CHOSEN, numberOrNullIfEmpty } from '../forms/fieldValues';
import { FormPanel } from '../forms/FormPanel';
import { QueryResult } from '../forms/QueryResult';
import { useProducts } from '../products/productQueries';
import { ORDER_PRIORITIES, priorityOfOption } from './orderValues';

interface OrderFormProps {
  /** The order to change, or null to create one. */
  order: OrderResponse | null;
  /** Gets the id of the saved order, which the caller reads again (DEC-29). */
  onSaved: (savedOrderId: number) => void;
  onClose: () => void;
}

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
        productId: numberOrNullIfEmpty(productId),
        quantity: numberOrNullIfEmpty(quantity),
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

  return (
    <FormPanel
      accessibleName={formName}
      heading={formName}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save"
      onSubmit={() => {
        saveMutation.mutate();
      }}
      onCancel={onClose}
      cancelLabel="Close"
    >
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
          setPriority(priorityOfOption(chosen));
        }}
        problem={problem}
        options={priorityOptions}
      />
    </FormPanel>
  );
}
