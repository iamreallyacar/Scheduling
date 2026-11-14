using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Routings;

internal readonly record struct StageLink(int PredecessorSequenceNo, int SuccessorSequenceNo, int MinLagMinutes);

// The rules a routing must keep as a whole (AC-35, AC-36). No database: everything is passed in.
internal static class RoutingRules
{
    public static bool AreNumberedFromOneWithoutGaps(IEnumerable<int> sequenceNumbers) =>
        sequenceNumbers.Order().Select((sequenceNo, position) => sequenceNo == position + 1).All(isInPlace => isInPlace);

    // Expects stages numbered 1 to stageCount.
    public static void RejectInvalidDependencies(int stageCount, IReadOnlyList<StageLink> links)
    {
        HashSet<(int, int)> seenPairs = [];
        foreach (StageLink link in links)
        {
            string linkName = $"The dependency from stage {link.PredecessorSequenceNo} to stage {link.SuccessorSequenceNo}";
            if (!IsStage(link.PredecessorSequenceNo) || !IsStage(link.SuccessorSequenceNo))
            {
                throw InvalidDependency($"{linkName} names a stage that is not in the routing.");
            }

            if (link.PredecessorSequenceNo == link.SuccessorSequenceNo)
            {
                throw InvalidDependency($"Stage {link.PredecessorSequenceNo} cannot depend on itself.");
            }

            if (link.MinLagMinutes < 0)
            {
                throw InvalidDependency($"{linkName} has a negative lag.");
            }

            if (!seenPairs.Add((link.PredecessorSequenceNo, link.SuccessorSequenceNo)))
            {
                throw InvalidDependency($"{linkName} is listed more than once.");
            }
        }

        if (FindCycle(stageCount, links) is { } cycle)
        {
            throw InvalidDependency($"The dependencies form a cycle through stages {string.Join(", ", cycle)}.");
        }

        bool IsStage(int sequenceNo) => sequenceNo >= 1 && sequenceNo <= stageCount;
    }

    // Depth-first walk along the dependencies. A stage met again while it is still on the
    // current path closes a cycle; the stages of that cycle are returned in walking order.
    private static List<int>? FindCycle(int stageCount, IReadOnlyList<StageLink> links)
    {
        ILookup<int, int> successorsByStage = links.ToLookup(link => link.PredecessorSequenceNo, link => link.SuccessorSequenceNo);
        HashSet<int> finishedStages = [];
        List<int> currentPath = [];

        List<int>? Walk(int stage)
        {
            int positionOnPath = currentPath.IndexOf(stage);
            if (positionOnPath >= 0)
            {
                return currentPath[positionOnPath..];
            }

            if (finishedStages.Contains(stage))
            {
                return null;
            }

            currentPath.Add(stage);
            foreach (int successor in successorsByStage[stage])
            {
                if (Walk(successor) is { } cycle)
                {
                    return cycle;
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            finishedStages.Add(stage);
            return null;
        }

        return Enumerable.Range(1, stageCount).Select(Walk).FirstOrDefault(cycle => cycle is not null);
    }

    private static ApiProblemException InvalidDependency(string detail) =>
        ApiProblemException.Rejected(StatusCodes.Status400BadRequest, ProblemCodes.RoutingInvalidDependency, detail);
}
