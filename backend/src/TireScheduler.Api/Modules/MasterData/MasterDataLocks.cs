using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.MasterData.Products;

namespace TireScheduler.Api.Modules.MasterData;

internal sealed class MasterDataLocks(MasterDataDbContext context, RequestTransaction requestTransaction) : IMasterDataLocks
{
    public async Task<bool> LockResourceAsync(int resourceId, CancellationToken cancellationToken)
    {
        requestTransaction.Join(context);
        return await context.LockRowAsync<Resource>(resourceId, cancellationToken);
    }

    public async Task<bool> LockProductAsync(int productId, CancellationToken cancellationToken)
    {
        requestTransaction.Join(context);
        return await context.LockRowAsync<Product>(productId, cancellationToken);
    }
}
