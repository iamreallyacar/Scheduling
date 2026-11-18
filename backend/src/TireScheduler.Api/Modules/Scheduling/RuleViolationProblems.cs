using TireScheduler.Api.Errors;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// A rejected placement or removal as problem details (API.md section "Error format", AC-109,
// AC-179): `rule` and `conflict` of the first violation in rule order, and every violation.
internal static class RuleViolationProblems
{
    public static ApiProblemException Rejection(IReadOnlyList<RuleViolation> violations)
    {
        RuleViolation first = violations.Count > 0
            ? violations[0]
            : throw new ArgumentException("A rejection needs at least one violation.", nameof(violations));
        return ApiProblemException.RuleViolated(first.Code, DetailOf(first), new Dictionary<string, object>
        {
            ["rule"] = first.Rule,
            ["conflict"] = ConflictOf(first.Conflict),
            ["violations"] = violations.Select(violation => new RuleViolationResponse
            {
                Rule = violation.Rule,
                Code = violation.Code,
                Conflict = ConflictOf(violation.Conflict),
                Detail = DetailOf(violation),
                EarliestAllowedStart = violation.EarliestAllowedStart,
            }).ToList(),
        });
    }

    private static ConflictResponse ConflictOf(ConflictItem conflict) => new()
    {
        Type = conflict.Kind switch
        {
            ConflictKind.Placement => "placement",
            ConflictKind.Unavailability => "unavailability",
            ConflictKind.Lot => "lot",
            ConflictKind.Resource => "resource",
            _ => throw new ArgumentOutOfRangeException(nameof(conflict), conflict.Kind, "Unknown conflict kind."),
        },
        Id = conflict.Id,
    };

    // The text the placement form shows for each violation; the times stay in the fields, so the
    // UI shows them on the plant's wall clock (DEC-40).
    private static string DetailOf(RuleViolation violation) => violation.Code switch
    {
        RuleCodes.ResourceType => "This machine is not of the type the lot's stage needs.",
        RuleCodes.Inactive => "This machine is inactive.",
        RuleCodes.Overlap => "The lot would overlap another placement on this machine.",
        RuleCodes.Unavailable => "The lot would overlap an unavailability of this machine.",
        RuleCodes.Dependency when violation.EarliestAllowedStart is not null => "The lot would start before a lot of an earlier stage ends, counting the lag.",
        RuleCodes.Dependency => "The lot would end too late for a placed lot of a later stage, counting the lag.",
        RuleCodes.OutsideWorkingTime => "The start is outside the machine's working time.",
        RuleCodes.Started => "The lot has started, so its placement cannot change.",
        RuleCodes.Cancelled => "The lot's order is cancelled, so the lot cannot be placed.",
        _ => throw new ArgumentOutOfRangeException(nameof(violation), violation.Code, "Unknown rule code."),
    };

    private sealed record ConflictResponse
    {
        public required string Type { get; init; }

        public required int Id { get; init; }
    }

    private sealed record RuleViolationResponse
    {
        public required string Rule { get; init; }

        public required string Code { get; init; }

        public required ConflictResponse Conflict { get; init; }

        public required string Detail { get; init; }

        public required DateTimeOffset? EarliestAllowedStart { get; init; }
    }
}
