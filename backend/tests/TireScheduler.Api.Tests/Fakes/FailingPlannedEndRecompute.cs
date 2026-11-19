using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Tests.Fakes;

// A recompute that fails, so a test can check that a working-time change saves nothing without it (AC-182).
internal sealed class FailingPlannedEndRecompute : IPlannedEndRecompute
{
    public Task RecomputeAllAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Planned ends cannot be recomputed (test).");
}
