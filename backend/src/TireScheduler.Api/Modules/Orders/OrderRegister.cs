using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Orders;

// F-ORD-1 and F-ORD-3: create, change, cancel and delete orders. Every change of an existing
// order locks its row first, so it runs one after the other with other changes and with
// placements of its lots (ARCHITECTURE.md section "Persistence rules").
internal sealed class OrderRegister(
    OrdersDbContext context,
    IMasterDataReader masterData,
    ILotPlacements lotPlacements,
    ILotProgress lotProgress,
    RequestTransaction requestTransaction,
    TimeProvider clock)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [OrdersDbContext.OrderNoIndex] = "orderNo" };

    public async Task<int> CreateAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        int productId = request.ProductId.Validated();
        int quantity = request.Quantity.Validated();
        GeneratedOperations generated = await GenerateAsync(productId, quantity, cancellationToken);

        Order order = new()
        {
            OrderNo = request.OrderNo.Trim(),
            CustomerName = request.CustomerName.Trim(),
            ProductId = productId,
            Quantity = quantity,
            DueDate = request.DueDate.Validated(),
            Priority = request.Priority ?? OrderPriority.Normal,
            CreatedAt = clock.GetUtcNow(),
        };
        order.Operations.AddRange(generated.Operations);
        order.Dependencies.AddRange(generated.Dependencies);
        context.Orders.Add(order);
        // One SaveChanges is one transaction: the order is saved with all its rows or not at all (AC-17).
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        return order.Id;
    }

    public async Task UpdateAsync(int orderId, OrderRequest request, CancellationToken cancellationToken)
    {
        int productId = request.ProductId.Validated();
        int quantity = request.Quantity.Validated();

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        Order order = await LockAndLoadAsync(orderId, cancellationToken);
        RejectCancelled(order);

        if (productId != order.ProductId || quantity != order.Quantity)
        {
            IReadOnlyCollection<int> lotIds = await LotIdsAsync(orderId, cancellationToken);
            if (await lotPlacements.AnyPlacedInLiveVersionAsync(lotIds, cancellationToken)
                || await lotProgress.AnyStartedAsync(lotIds, cancellationToken))
            {
                throw ApiProblemException.Rejected(
                    StatusCodes.Status409Conflict,
                    ProblemCodes.OrderLocked,
                    "The product and quantity cannot change while a lot of this order is placed or started (A1).");
            }

            // AC-48: generated again from the product's current routing. The lots and
            // dependencies go with their operations (cascade).
            GeneratedOperations generated = await GenerateAsync(productId, quantity, cancellationToken);
            await context.Operations.Where(operation => operation.OrderId == orderId).ExecuteDeleteAsync(cancellationToken);
            order.Operations.AddRange(generated.Operations);
            order.Dependencies.AddRange(generated.Dependencies);
            order.ProductId = productId;
            order.Quantity = quantity;
        }

        order.OrderNo = request.OrderNo.Trim();
        order.CustomerName = request.CustomerName.Trim();
        order.DueDate = request.DueDate.Validated();
        order.Priority = request.Priority ?? OrderPriority.Normal;
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // AC-50, AC-51. The placements of its lots that have not started leave the schedule in the
    // same transaction, so they stop blocking machines (A13, DEC-33).
    public async Task CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        Order order = await LockAndLoadAsync(orderId, cancellationToken);
        RejectCancelled(order);
        IReadOnlyCollection<int> lotIds = await LotIdsAsync(orderId, cancellationToken);
        if (await lotProgress.AnyInProgressAsync(lotIds, cancellationToken))
        {
            throw ApiProblemException.Rejected(
                StatusCodes.Status409Conflict,
                ProblemCodes.OrderLotInProgress,
                "This order cannot be cancelled while one of its lots is in progress.");
        }

        order.CancelledAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        await lotPlacements.RemoveUnstartedAsync(lotIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // A13: only a Pending order with no placement and no progress, for orders entered by mistake.
    public async Task DeleteAsync(int orderId, CancellationToken cancellationToken)
    {
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        Order order = await LockAndLoadAsync(orderId, cancellationToken);
        IReadOnlyCollection<int> lotIds = await LotIdsAsync(orderId, cancellationToken);
        if (order.CancelledAt is not null
            || await lotPlacements.AnyPlacedInLiveVersionAsync(lotIds, cancellationToken)
            || await lotProgress.AnyStartedAsync(lotIds, cancellationToken))
        {
            throw ApiProblemException.Rejected(
                StatusCodes.Status409Conflict,
                ProblemCodes.OrderNotDeletable,
                "Only a pending order with no placed or started lot can be deleted. Cancel this order instead.");
        }

        order.IsDeleted = true;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<GeneratedOperations> GenerateAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        ProductRouting routing = await masterData.FindProductRoutingAsync(productId, cancellationToken)
            ?? throw ApiProblemException.ValidationFailed("productId", "This product does not exist.");
        if (routing.Stages.Count == 0)
        {
            // AC-46: without stages there is nothing to schedule.
            throw ApiProblemException.ValidationFailed("productId", "This product has no routing yet, so no operations can be generated for it.");
        }

        return OperationGenerator.Generate(routing, quantity);
    }

    // Holds the order row until the transaction ends. A soft-deleted order is not found (AC-23).
    private async Task<Order> LockAndLoadAsync(int orderId, CancellationToken cancellationToken)
    {
        bool isLocked = await context.LockRowAsync<Order>(orderId, cancellationToken);
        Order? order = isLocked ? await context.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken) : null;
        return order ?? throw ApiProblemException.NotFound("This order");
    }

    private async Task<IReadOnlyCollection<int>> LotIdsAsync(int orderId, CancellationToken cancellationToken) =>
        await context.Operations
            .Where(operation => operation.OrderId == orderId)
            .SelectMany(operation => operation.Lots)
            .Select(lot => lot.Id)
            .ToListAsync(cancellationToken);

    private static void RejectCancelled(Order order)
    {
        if (order.CancelledAt is not null)
        {
            throw ApiProblemException.Rejected(StatusCodes.Status409Conflict, ProblemCodes.OrderCancelled, "This order is cancelled and cannot change any more.");
        }
    }
}
