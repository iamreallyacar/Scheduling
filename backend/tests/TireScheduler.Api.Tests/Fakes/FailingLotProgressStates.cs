using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Tests.Fakes;

// Progress that cannot be read, so a test can check that a write which needs it saves nothing (AC-161).
internal sealed class FailingLotProgressStates : ILotProgressStates
{
    public Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Progress cannot be read (test).");

    public Task<IReadOnlyDictionary<int, DateTimeOffset>> ActualEndsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Progress cannot be read (test).");
}
