using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Queries.Machines;

// The machine reads (DEC-39): what MasterData stores, with the status derived at the current time.
internal sealed class MachineViewReader(IMasterDataReader masterData, IUnavailabilityReader unavailabilities, TimeProvider clock)
{
    public async Task<IReadOnlyList<ResourceViewResponse>> ListAsync(int? resourceTypeId, bool? isActive, CancellationToken cancellationToken) =>
        await WithStatusAsync(await masterData.ListResourcesAsync(resourceTypeId, isActive, cancellationToken), cancellationToken);

    public async Task<ResourceViewResponse> GetAsync(int resourceId, CancellationToken cancellationToken) =>
        (await WithStatusAsync([await masterData.GetResourceAsync(resourceId, cancellationToken)], cancellationToken))[0];

    private async Task<IReadOnlyList<ResourceViewResponse>> WithStatusAsync(IReadOnlyList<ResourceResponse> resources, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, IReadOnlyList<UnavailabilityKind>> kindsCoveringNow = await unavailabilities.KindsCoveringAsync(
            [.. resources.Select(resource => resource.Id)],
            clock.GetUtcNow(),
            cancellationToken);
        return
        [
            .. resources.Select(resource => new ResourceViewResponse
            {
                Id = resource.Id,
                Name = resource.Name,
                ResourceType = resource.ResourceType,
                Calendar = resource.Calendar,
                IsActive = resource.IsActive,
                Status = ResourceStatusRules.Derive(kindsCoveringNow.GetValueOrDefault(resource.Id, [])),
            }),
        ];
    }
}
