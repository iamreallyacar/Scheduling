namespace TireScheduler.Api.Modules.MasterData;

// The public read interface of MasterData for the modules above it (ARCHITECTURE.md section "Module rules").
public interface IMasterDataReader
{
    // The IANA id of the plant time zone (ARCHITECTURE.md section "Time rules").
    string PlantTimeZoneId { get; }

    // Null when the product does not exist. A product without a routing has no stages (A24).
    Task<ProductRouting?> FindProductRoutingAsync(int productId, CancellationToken cancellationToken);

    // The products whose code or name contains the text without regard to case (A29).
    Task<IReadOnlyList<int>> FindProductIdsContainingAsync(string text, CancellationToken cancellationToken);

    // Only the ids that exist are in the answer.
    Task<IReadOnlyDictionary<int, ProductReference>> GetProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken);

    // Only the ids that exist are in the answer.
    Task<IReadOnlyDictionary<int, NamedReference>> GetResourceTypesAsync(IReadOnlyCollection<int> resourceTypeIds, CancellationToken cancellationToken);
}

public sealed record ProductReference
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}

// Stages in sequence order; dependencies name their stages by sequence number (A24).
public sealed record ProductRouting
{
    public required IReadOnlyList<RoutingStageValues> Stages { get; init; }

    public required IReadOnlyList<RoutingLinkValues> Dependencies { get; init; }
}

public sealed record RoutingStageValues
{
    public required int SequenceNo { get; init; }

    public required int ResourceTypeId { get; init; }

    public required int SetupMinutes { get; init; }

    public required int CycleMinutes { get; init; }

    public required int UnitsPerCycle { get; init; }
}

public sealed record RoutingLinkValues
{
    public required int PredecessorSequenceNo { get; init; }

    public required int SuccessorSequenceNo { get; init; }

    public required int MinLagMinutes { get; init; }
}
