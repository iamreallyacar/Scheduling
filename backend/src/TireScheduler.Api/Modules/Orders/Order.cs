namespace TireScheduler.Api.Modules.Orders;

// A customer request for a quantity of a product by a due date (domain-model.md section 2).
// Status is derived in Queries, never stored (DEC-12).
internal sealed class Order
{
    public const int OrderNoMaxLength = 50;
    public const int CustomerNameMaxLength = 200;

    public int Id { get; private set; }

    public required string OrderNo { get; set; }

    public required string CustomerName { get; set; }

    // A MasterData product. No foreign key: modules do not reach into each other's schemas.
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    // Plant-local; due at the end of that day (DEC-22).
    public DateOnly DueDate { get; set; }

    public OrderPriority Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    // Soft delete for orders entered by mistake (A13); hidden by a global query filter.
    public bool IsDeleted { get; set; }

    public List<Operation> Operations { get; } = [];

    public List<OperationDependency> Dependencies { get; } = [];
}
