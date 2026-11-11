import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { createProduct, updateProduct } from '../api/endpoints';
import type { ProductResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { productsQueryKey } from './productQueries';

interface ProductFormProps {
  /** The product to change, or null to add one. */
  product: ProductResponse | null;
  onClose: () => void;
}

export function ProductForm({ product, onClose }: ProductFormProps) {
  const queryClient = useQueryClient();
  const [code, setCode] = useState(product?.code ?? '');
  const [name, setName] = useState(product?.name ?? '');
  const [spec, setSpec] = useState(product?.spec ?? '');

  const saveMutation = useMutation({
    mutationFn: () => {
      const request = { code, name, spec };
      return product === null ? createProduct(request) : updateProduct(product.id, request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: productsQueryKey });
      onClose();
    },
  });
  const problem = problemOf(saveMutation.error);

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  return (
    <form className="panel" onSubmit={submit} noValidate aria-label={product === null ? 'Add product' : `Edit product ${product.code}`}>
      <h3>{product === null ? 'Add product' : `Edit ${product.code}`}</h3>
      <ProblemSummary problem={problem} />
      <TextField label="Code" name="code" value={code} onChange={setCode} problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <TextField label="Size or spec (optional)" name="spec" value={spec} onChange={setSpec} problem={problem} />
      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save
        </button>
        <button type="button" onClick={onClose}>
          Cancel
        </button>
      </div>
    </form>
  );
}
