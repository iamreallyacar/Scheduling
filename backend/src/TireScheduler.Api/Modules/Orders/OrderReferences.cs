using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Orders;

// Tells MasterData which order uses a product or resource type (A14, AC-20). Deleted orders are
// left out by the query filter: nobody can see them, so they must not block a delete.
internal sealed class OrderReferences(OrdersDbContext context) : IProductReferences, IResourceTypeReferences
{
    public async Task<string?> FindItemUsingProductAsync(int productId, CancellationToken cancellationToken) =>
        Describe(await context.Orders
            .Where(order => order.ProductId == productId)
            .OrderBy(order => order.OrderNo)
            .Select(order => order.OrderNo)
            .FirstOrDefaultAsync(cancellationToken));

    public async Task<string?> FindItemUsingResourceTypeAsync(int resourceTypeId, CancellationToken cancellationToken) =>
        Describe(await context.Orders
            .Where(order => order.Operations.Any(operation => operation.ResourceTypeId == resourceTypeId))
            .OrderBy(order => order.OrderNo)
            .Select(order => order.OrderNo)
            .FirstOrDefaultAsync(cancellationToken));

    private static string? Describe(string? orderNo) => orderNo is null ? null : $"order '{orderNo}'";
}
