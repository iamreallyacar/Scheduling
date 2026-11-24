using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Modules.Progress;

// Answers the ports of Orders and Scheduling (DEC-16) and the reads of Queries (DEC-62) from the
// stored progress. A lot is started once it has a row and completed once the row has an end.
internal sealed class LotProgressFacts(ProgressDbContext context) : ILotProgress, ILotProgressStates, ILotProgressReader
{
    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        context.LotProgress.AnyAsync(progress => lotIds.Contains(progress.LotId), cancellationToken);

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        context.LotProgress.AnyAsync(progress => lotIds.Contains(progress.LotId) && progress.ActualEnd == null, cancellationToken);

    public async Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        await context.LotProgress
            .Where(progress => lotIds.Contains(progress.LotId))
            .ToDictionaryAsync(
                progress => progress.LotId,
                progress => progress.ActualEnd == null ? StartedLotState.InProgress : StartedLotState.Completed,
                cancellationToken);

    public async Task<IReadOnlyDictionary<int, DateTimeOffset>> ActualEndsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        Dictionary<int, DateTimeOffset> actualEnds = [];
        foreach (LotProgress progress in await context.LotProgress.Where(progress => lotIds.Contains(progress.LotId)).ToListAsync(cancellationToken))
        {
            if (progress.ActualEnd is { } actualEnd)
            {
                actualEnds[progress.LotId] = actualEnd;
            }
        }

        return actualEnds;
    }

    public async Task<IReadOnlyDictionary<int, LotActuals>> ActualsOfLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        await context.LotProgress
            .Where(progress => lotIds.Contains(progress.LotId))
            .ToDictionaryAsync(
                progress => progress.LotId,
                progress => new LotActuals { ActualStart = progress.ActualStart, ActualEnd = progress.ActualEnd },
                cancellationToken);

    public async Task<IReadOnlyList<int>> InProgressLotIdsAsync(CancellationToken cancellationToken) =>
        await context.LotProgress
            .Where(progress => progress.ActualEnd == null)
            .Select(progress => progress.LotId)
            .ToListAsync(cancellationToken);
}
