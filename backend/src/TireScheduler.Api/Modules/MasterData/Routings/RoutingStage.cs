using TireScheduler.Api.Modules.MasterData.Machines;

namespace TireScheduler.Api.Modules.MasterData.Routings;

// One step of a product's routing. Its id lives only until the routing is saved again, so
// nothing outside the routing points to it; orders copy the values (A1).
internal sealed class RoutingStage
{
    public int Id { get; private set; }

    public int ProductId { get; set; }

    public int SequenceNo { get; set; }

    public int ResourceTypeId { get; set; }

    public ResourceType? ResourceType { get; set; }

    public int SetupMinutes { get; set; }

    public int CycleMinutes { get; set; }

    public int UnitsPerCycle { get; set; }
}
