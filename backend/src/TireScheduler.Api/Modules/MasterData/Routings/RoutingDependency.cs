namespace TireScheduler.Api.Modules.MasterData.Routings;

// The successor stage may start only MinLagMinutes after the predecessor stage ends.
internal sealed class RoutingDependency
{
    public int ProductId { get; set; }

    public int PredecessorStageId { get; set; }

    public RoutingStage? PredecessorStage { get; set; }

    public int SuccessorStageId { get; set; }

    public RoutingStage? SuccessorStage { get; set; }

    public int MinLagMinutes { get; set; }
}
