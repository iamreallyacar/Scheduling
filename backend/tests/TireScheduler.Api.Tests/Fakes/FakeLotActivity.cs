using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Tests.Fakes;

// Stands in for Progress behind the ports of Orders and Scheduling (DEC-16), so the rules that
// depend on started lots are tested on their own, also in states the API cannot reach, such as a
// completed lot without a live placement (DEC-62). Queries still reads the real Progress.
internal sealed class FakeLotActivity : ILotProgress, ILotProgressStates
{
    public HashSet<int> StartedLotIds { get; } = [];

    public HashSet<int> CompletedLotIds { get; } = [];

    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(StartedLotIds.Contains));

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(lotId => StartedLotIds.Contains(lotId) && !CompletedLotIds.Contains(lotId)));

    public Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, StartedLotState>>(lotIds
            .Where(StartedLotIds.Contains)
            .ToDictionary(lotId => lotId, lotId => CompletedLotIds.Contains(lotId) ? StartedLotState.Completed : StartedLotState.InProgress));

    // The fake keeps no actual times, so a completed lot has no actual end and an order never has
    // a completion time.
    public Task<IReadOnlyDictionary<int, DateTimeOffset>> ActualEndsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, DateTimeOffset>>(new Dictionary<int, DateTimeOffset>());

    // Replaces Progress in an API under test.
    public void ReplaceProgressIn(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILotProgress>(this));
        services.Replace(ServiceDescriptor.Singleton<ILotProgressStates>(this));
    }
}
