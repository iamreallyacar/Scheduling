using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Queries.Machines;

// A machine as read, with its derived status (DEC-39). The writes answer with MasterData's
// ResourceResponse, which has no status (AC-28).
public sealed record ResourceViewResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required NamedReference Calendar { get; init; }

    public required bool IsActive { get; init; }

    public required ResourceStatus Status { get; init; }
}
