namespace TireScheduler.Api.Modules.MasterData.Routings;

// The one order of a routing's rows. It decides the order of the operations generated for an
// order and of the routing answer, so both read through these and cannot drift apart.
internal static class RoutingOrdering
{
    public static IQueryable<RoutingStage> StagesOf(this MasterDataDbContext context, int productId) =>
        context.RoutingStages
            .Where(stage => stage.ProductId == productId)
            .OrderBy(stage => stage.SequenceNo);

    public static IQueryable<RoutingDependency> DependenciesOf(this MasterDataDbContext context, int productId) =>
        context.RoutingDependencies
            .Where(dependency => dependency.ProductId == productId)
            .OrderBy(dependency => dependency.SuccessorStage!.SequenceNo)
            .ThenBy(dependency => dependency.PredecessorStage!.SequenceNo);
}
