using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Tests.Fakes;

// Stands in for Progress behind the ports of Orders and Scheduling (DEC-16) until Progress stores
// actual starts and ends (task M5-1), so the rules that depend on started lots can be tested now.
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

    // Replaces Progress in an API under test.
    public void ReplaceProgressIn(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILotProgress>(this));
        services.Replace(ServiceDescriptor.Singleton<ILotProgressStates>(this));
    }
}
