using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;
namespace TireScheduler.Api.Modules.Progress;

// F-PRG-1: records, corrects and removes the progress of a lot (DEC-62, A47). Every write locks
// the order row first, as changing or cancelling the order and every placement write do, so the
// checks on the live placement and the cancellation cannot interleave with them (AC-211). It
// takes no resource lock, so it cannot wait on a placement that holds the resource and the order.
internal sealed class ProgressRegister(
    ProgressDbContext context,
    IOrderReader orders,
    IOrderLocks orderLocks,
    IPlacementReader placements,
    RequestTransaction requestTransaction,
    TimeProvider clock)
{
    public async Task<LotProgressResponse> RecordAsync(int lotId, LotProgressRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset actualStart = request.ActualStart.Validated().ToUniversalTime();
        DateTimeOffset? actualEnd = request.ActualEnd?.ToUniversalTime();
        ThrowOnUnfitTimes(actualStart, actualEnd);
        int orderId = await orders.FindOrderIdOfLotAsync(lotId, cancellationToken) ?? throw LotNotFound();

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        OrderSchedulingFacts order = await LockOrderOfLotAsync(orderId, lotId, cancellationToken);
        // The lot runs on its live placement's machine (domain-model.md section 2).
        IReadOnlyDictionary<int, PlacementValues> livePlacements = await placements.PlacementsOfLotsAsync(ScheduleVersionAlias.Live, [lotId], cancellationToken);
        if (!ProgressRules.CanRecordProgress(livePlacements.ContainsKey(lotId), order.IsCancelled))
        {
            throw order.IsCancelled
                ? OrderCancelled()
                : ApiProblemException.Rejected(
                    StatusCodes.Status409Conflict,
                    ProblemCodes.ProgressNotPlaced,
                    "This lot is not placed in the live schedule, so it cannot have started. Place it first.");
        }

        LotProgress? progress = await context.LotProgress.FindAsync([lotId], cancellationToken);
        if (progress is null)
        {
            progress = new LotProgress { LotId = lotId };
            context.LotProgress.Add(progress);
        }

        progress.ActualStart = actualStart;
        progress.ActualEnd = actualEnd;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LotProgressResponse { LotId = lotId, ActualStart = progress.ActualStart, ActualEnd = progress.ActualEnd };
    }

    // For progress entered by mistake; the lot is then Scheduled again (A47).
    public async Task RemoveAsync(int lotId, CancellationToken cancellationToken)
    {
        int orderId = await orders.FindOrderIdOfLotAsync(lotId, cancellationToken) ?? throw LotNotFound();

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        if ((await LockOrderOfLotAsync(orderId, lotId, cancellationToken)).IsCancelled)
        {
            throw OrderCancelled();
        }

        LotProgress progress = await context.LotProgress.FindAsync([lotId], cancellationToken)
            ?? throw ApiProblemException.NotFound("Progress of this lot");
        context.LotProgress.Remove(progress);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Actual times are facts, so they are checked only for what no fact can be (A47).
    private void ThrowOnUnfitTimes(DateTimeOffset actualStart, DateTimeOffset? actualEnd)
    {
        DateTimeOffset now = clock.GetUtcNow();
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        if (ErrorOf(actualStart, "actual start", now) is { } startError)
        {
            fieldErrors["actualStart"] = [startError];
        }

        if (actualEnd is { } end)
        {
            string? endError = ErrorOf(end, "actual end", now)
                ?? (end <= actualStart ? "The actual end must be after the actual start." : null);
            if (endError is not null)
            {
                fieldErrors["actualEnd"] = [endError];
            }
        }

        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }
    }

    private static string? ErrorOf(DateTimeOffset time, string timeName, DateTimeOffset now) =>
        WholeMinuteTime.ErrorOf(time, timeName)
        ?? (time > now ? $"The {timeName} cannot be after now; record it once it has happened." : null);

    // Read again under the lock: the order may have been cancelled or deleted, or its lots generated
    // again, since the lot was looked up. A cancelled order no longer changes (A47).
    private async Task<OrderSchedulingFacts> LockOrderOfLotAsync(int orderId, int lotId, CancellationToken cancellationToken)
    {
        OrderSchedulingFacts? order = await orderLocks.LockOrderAsync(orderId, cancellationToken)
            ? await orders.FindSchedulingFactsAsync(orderId, cancellationToken)
            : null;
        if (order is null || order.Lots.All(lot => lot.Id != lotId))
        {
            throw LotNotFound();
        }

        return order;
    }

    private static ApiProblemException OrderCancelled() => ApiProblemException.Rejected(
        StatusCodes.Status409Conflict,
        ProblemCodes.OrderCancelled,
        "The order of this lot is cancelled, so its progress cannot change any more.");

    private static ApiProblemException LotNotFound() => ApiProblemException.NotFound("This lot");
}
