namespace TireScheduler.Api.Modules.Orders;

// A schedulable part of an operation. v1 creates exactly one lot per operation (A6).
internal sealed class Lot
{
    public int Id { get; private set; }

    public int OperationId { get; set; }

    public int LotNo { get; set; }

    public int Quantity { get; set; }

    // Computed when the lot is created (domain-model.md section 4).
    public int DurationMinutes { get; set; }
}
