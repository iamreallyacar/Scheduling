namespace TireScheduler.Api.Modules.Scheduling;

// A period when a resource cannot run lots (domain-model.md section 2). Shared by all schedule
// versions (A9). A breakdown with no known end has no end (DEC-41).
internal sealed class Unavailability
{
    public const int ReasonMaxLength = 200;

    public int Id { get; private set; }

    // A MasterData resource. No foreign key: modules do not reach into each other's schemas.
    public int ResourceId { get; init; }

    public UnavailabilityKind Kind { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public string? Reason { get; set; }
}
