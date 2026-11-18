import { Link, useParams } from 'react-router';
import { useSignedInUser } from '../auth/signedInUser';
import { QueryResult } from '../forms/QueryResult';
import { useResourceTypes } from '../machines/machineQueries';
import { NotFoundPage } from '../shell/NotFoundPage';
import { RoutingEditor } from './RoutingEditor';
import { useProduct, useRouting } from './routingQueries';
import { RoutingTables } from './RoutingTables';

// F-MD-3: the routing of one product. Everyone reads; an Admin edits.
export function RoutingPage() {
  const productId = Number(useParams()['productId']);
  return Number.isInteger(productId) ? <ProductRouting productId={productId} /> : <NotFoundPage />;
}

function ProductRouting({ productId }: { productId: number }) {
  const { canAdminister } = useSignedInUser();
  const product = useProduct(productId);
  const routing = useRouting(productId);
  const resourceTypes = useResourceTypes();

  return (
    <section aria-labelledby="routing-heading">
      <QueryResult query={product}>
        {(loadedProduct) => (
          <>
            <h1 id="routing-heading">Routing of {loadedProduct.code}</h1>
            <p className="page-message">{loadedProduct.name}</p>
            <QueryResult query={routing}>
              {(loadedRouting) =>
                canAdminister ? (
                  <QueryResult query={resourceTypes}>
                    {(loadedTypes) => <RoutingEditor productId={productId} routing={loadedRouting} resourceTypes={loadedTypes} />}
                  </QueryResult>
                ) : (
                  <RoutingTables routing={loadedRouting} />
                )
              }
            </QueryResult>
          </>
        )}
      </QueryResult>
      <p>
        <Link to="/products">Back to products</Link>
      </p>
    </section>
  );
}
