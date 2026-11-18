namespace TireScheduler.Api.Queries.Orders;

// domain-model.md section 6. Derived on every read, never stored (DEC-12).
public enum LotStatus
{
    Unscheduled,
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}
