namespace TireScheduler.Api.Modules.Orders;

// A schedulable part of an operation. v1 creates exactly one lot per operation (A6).
internal sealed class Lot
{
    // One year (DEC-69): far longer than any real lot, and short enough that the working time
    // walk of a search stays far from the end of the calendar.
    public const int MaxDurationMinutes = 525_600;

    public int Id { get; private set; }

    public int OperationId { get; set; }

    public int LotNo { get; set; }

    public int Quantity { get; set; }

    // Computed when the lot is created (domain-model.md section 4).
    public int DurationMinutes { get; set; }
}
