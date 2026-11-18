namespace TireScheduler.Api.Queries.Orders;

// Order progress (domain-model.md section 6): completed quantity over the total quantity of the
// lots of all operations, so lots of different sizes count fairly.
internal static class OrderProgressRules
{
    public static OrderProgressResponse Measure(IEnumerable<OperationResponse> operations)
    {
        LotResponse[] lots = operations.SelectMany(operation => operation.Lots).ToArray();
        return new OrderProgressResponse
        {
            CompletedQuantity = lots.Where(lot => lot.Status == LotStatus.Completed).Sum(lot => lot.Quantity),
            TotalQuantity = lots.Sum(lot => lot.Quantity),
        };
    }
}
