namespace TireScheduler.Api.Modules.Orders;

// The work for one routing stage of one order. The stage values are copied, not referenced,
// so a later routing change does not change the order (A1).
internal sealed class Operation
{
    public int Id { get; private set; }

    public int OrderId { get; set; }

    public int SequenceNo { get; set; }

    // A MasterData resource type, without a foreign key (see Order.ProductId).
    public int ResourceTypeId { get; set; }

    public int Quantity { get; set; }

    public int SetupMinutes { get; set; }

    public int CycleMinutes { get; set; }

    public int UnitsPerCycle { get; set; }

    public List<Lot> Lots { get; } = [];
}
