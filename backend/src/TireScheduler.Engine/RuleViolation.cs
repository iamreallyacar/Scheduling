namespace TireScheduler.Engine;

// The stable codes of API.md section "Error format", one per way a rule can be broken.
public static class RuleCodes
{
    public const string ResourceType = "rule.R1.resource_type";
    public const string Inactive = "rule.R1.inactive";
    public const string Overlap = "rule.R2.overlap";
    public const string Unavailable = "rule.R3.unavailable";
    public const string Dependency = "rule.R4.dependency";
    public const string OutsideWorkingTime = "rule.R6.outside_working_time";
    public const string Started = "rule.R8.started";
    public const string Cancelled = "rule.R8.cancelled";
}

public enum ConflictKind
{
    Placement,
    Unavailability,
    Lot,
    Resource,
}

public sealed record ConflictItem(ConflictKind Kind, int Id);

// One broken rule and the item it conflicts with (R7). EarliestAllowedStart is set only when a
// placed predecessor lot holds the start back (AC-91).
public sealed record RuleViolation(string Rule, string Code, ConflictItem Conflict, DateTimeOffset? EarliestAllowedStart);

// A placement is accepted when there are no violations; PlannedEnd is then always set. It is
// null only when the start is outside working time, because no planned end exists then (A35).
public sealed record PlacementCheck(DateTimeOffset? PlannedEnd, IReadOnlyList<RuleViolation> Violations)
{
    public bool IsAccepted => Violations.Count == 0;
}

public sealed record PlacementRequest(int LotId, int ResourceId, DateTimeOffset PlannedStart);

public sealed record PlacementConflict(int PlacementId, int LotId, IReadOnlyList<RuleViolation> Violations);
