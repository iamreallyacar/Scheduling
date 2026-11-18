using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Orders;

internal sealed record GeneratedOperations
{
    public required IReadOnlyList<Operation> Operations { get; init; }

    public required IReadOnlyList<OperationDependency> Dependencies { get; init; }
}

// F-ORD-3: one operation per routing stage with the stage values copied (A1), the dependencies
// copied with their lags, and one lot per operation with the whole quantity (A6).
internal static class OperationGenerator
{
    private const int FirstLotNo = 1;

    // Expects a routing with at least one stage.
    public static GeneratedOperations Generate(ProductRouting routing, int quantity)
    {
        Dictionary<int, Operation> operationBySequenceNo = routing.Stages.ToDictionary(
            stage => stage.SequenceNo,
            stage =>
            {
                Operation operation = new()
                {
                    SequenceNo = stage.SequenceNo,
                    ResourceTypeId = stage.ResourceTypeId,
                    Quantity = quantity,
                    SetupMinutes = stage.SetupMinutes,
                    CycleMinutes = stage.CycleMinutes,
                    UnitsPerCycle = stage.UnitsPerCycle,
                };
                operation.Lots.Add(new Lot
                {
                    LotNo = FirstLotNo,
                    Quantity = quantity,
                    DurationMinutes = StoredDurationMinutes(stage, quantity),
                });
                return operation;
            });
        OperationDependency[] dependencies = routing.Dependencies
            .Select(link => new OperationDependency
            {
                PredecessorOperation = operationBySequenceNo[link.PredecessorSequenceNo],
                SuccessorOperation = operationBySequenceNo[link.SuccessorSequenceNo],
                MinLagMinutes = link.MinLagMinutes,
            })
            .ToArray();
        return new GeneratedOperations { Operations = operationBySequenceNo.Values.ToArray(), Dependencies = dependencies };
    }

    // AC-145: a duration is stored as a 32-bit number of minutes.
    private static int StoredDurationMinutes(RoutingStageValues stage, int quantity)
    {
        long minutes = LotDuration.Minutes(stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle, quantity);
        return minutes <= int.MaxValue
            ? (int)minutes
            : throw ApiProblemException.ValidationFailed(
                "quantity",
                $"With this quantity a lot of stage {stage.SequenceNo} would take {minutes} minutes, more than the longest duration that can be stored ({int.MaxValue} minutes).");
    }
}
