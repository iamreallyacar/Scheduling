namespace TireScheduler.Engine.Tests;

internal static class RuleAssert
{
    public static void Accepted(PlacementCheck check)
    {
        Assert.Empty(check.Violations);
        Assert.NotNull(check.PlannedEnd);
    }

    // The first violation is the one the API answers with in `rule` and `conflict` (AC-110).
    public static RuleViolation RejectedFirst(PlacementCheck check, string expectedCode, ConflictKind expectedKind, int expectedConflictId)
    {
        Assert.NotEmpty(check.Violations);
        RuleViolation first = check.Violations[0];
        Assert.Equal(expectedCode, first.Code);
        Assert.Equal(new ConflictItem(expectedKind, expectedConflictId), first.Conflict);
        return first;
    }
}
