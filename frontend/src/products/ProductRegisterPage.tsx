import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { deleteProduct, listProducts } from '../api/endpoints';
import type { ProductResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { useSignedInUser } from '../auth/signedInUser';
import { ProblemSummary } from '../forms/ProblemSummary';
import { QueryResult } from '../forms/QueryResult';
import { ProductForm } from './ProductForm';
import { productsQueryKey } from './productQueries';

// F-MD-2: the product (tire model) register. Everyone reads; an Admin edits.
export function ProductRegisterPage() {
  const { canAdminister } = useSignedInUser();
  const [productInForm, setProductInForm] = useState<ProductResponse | 'new' | null>(null);
  const products = useQuery({ queryKey: productsQueryKey, queryFn: listProducts });
  const queryClient = useQueryClient();
  const deleteMutation = useMutation({
    mutationFn: deleteProduct,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: productsQueryKey }),
  });

  return (
    <section aria-labelledby="products-heading">
      <h1 id="products-heading">Products</h1>
      <ProblemSummary problem={problemOf(deleteMutation.error)} />
      <QueryResult query={products}>
        {(listedProducts) =>
          listedProducts.length === 0 ? (
            <p className="page-message">No products yet.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Name</th>
                  <th scope="col">Size or spec</th>
                  <th scope="col">Routing</th>
                  {canAdminister && <th scope="col">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {listedProducts.map((product) => (
                  <tr key={product.id}>
                    <th scope="row">{product.code}</th>
                    <td>{product.name}</td>
                    <td>{product.spec}</td>
                    <td>
                      <Link to={`/products/${String(product.id)}/routing`} aria-label={`Routing of ${product.code}`}>
                        {canAdminister ? 'Edit routing' : 'View routing'}
                      </Link>
                    </td>
                    {canAdminister && (
                      <td className="row-actions">
                        <button
                          type="button"
                          aria-label={`Edit ${product.code}`}
                          onClick={() => {
                            setProductInForm(product);
                          }}
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          aria-label={`Delete ${product.code}`}
                          disabled={deleteMutation.isPending}
                          onClick={() => {
                            deleteMutation.mutate(product.id);
                          }}
                        >
                          Delete
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )
        }
      </QueryResult>
      {canAdminister && productInForm === null && (
        <button
          type="button"
          onClick={() => {
            setProductInForm('new');
          }}
        >
          Add product
        </button>
      )}
      {productInForm !== null && (
        <ProductForm
          // A new key gives the form fresh state when another product is chosen.
          key={productInForm === 'new' ? 'new' : productInForm.id}
          product={productInForm === 'new' ? null : productInForm}
          onClose={() => {
            setProductInForm(null);
          }}
        />
      )}
    </section>
  );
}
