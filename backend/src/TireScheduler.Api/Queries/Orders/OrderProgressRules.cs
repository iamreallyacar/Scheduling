namespace TireScheduler.Api.Queries.Orders;

// Order progress (domain-model.md section 6): completed quantity over the total quantity of the
// lots of all operations, so lots of different sizes count fairly. The percentage is rounded down
// so that it reads 100 only once every lot is completed (DEC-64).
internal static class OrderProgressRules
{
    public static OrderProgressResponse Measure(IEnumerable<OperationResponse> operations)
    {
        LotResponse[] lots = operations.SelectMany(operation => operation.Lots).ToArray();
        int completedQuantity = lots.Where(lot => lot.Status == LotStatus.Completed).Sum(lot => lot.Quantity);
        // Every order has a lot of quantity 1 or more (AC-45, AC-46), so the total is never 0.
        int totalQuantity = lots.Sum(lot => lot.Quantity);
        return new OrderProgressResponse
        {
            CompletedQuantity = completedQuantity,
            TotalQuantity = totalQuantity,
            PercentComplete = completedQuantity * 100 / totalQuantity,
        };
    }
}
