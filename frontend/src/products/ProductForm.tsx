import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { createProduct, updateProduct } from '../api/generated/endpoints.gen';
import type { ProductResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { TextField } from '../forms/Fields';
import { FormPanel } from '../forms/FormPanel';

interface ProductFormProps {
  /** The product to change, or null to add one. */
  product: ProductResponse | null;
  onClose: () => void;
}

export function ProductForm({ product, onClose }: ProductFormProps) {
  const [code, setCode] = useState(product?.code ?? '');
  const [name, setName] = useState(product?.name ?? '');
  const [spec, setSpec] = useState(product?.spec ?? '');

  const saveMutation = useMutation({
    mutationFn: () => {
      const request = { code, name, spec };
      return product === null ? createProduct(request) : updateProduct(product.id, request);
    },
    onSuccess: onClose,
  });
  const problem = problemOf(saveMutation.error);

  return (
    <FormPanel
      accessibleName={product === null ? 'Add product' : `Edit product ${product.code}`}
      heading={product === null ? 'Add product' : `Edit ${product.code}`}
      problem={problem}
      pending={saveMutation.isPending}
      submitLabel="Save"
      onSubmit={() => {
        saveMutation.mutate();
      }}
      onCancel={onClose}
    >
      <TextField label="Code" name="code" value={code} onChange={setCode} problem={problem} />
      <TextField label="Name" name="name" value={name} onChange={setName} problem={problem} />
      <TextField label="Size or spec (optional)" name="spec" value={spec} onChange={setSpec} problem={problem} />
    </FormPanel>
  );
}
