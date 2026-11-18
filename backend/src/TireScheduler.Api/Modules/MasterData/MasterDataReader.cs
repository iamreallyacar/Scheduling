using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData.Calendars;
using TireScheduler.Api.Modules.MasterData.Machines;

namespace TireScheduler.Api.Modules.MasterData;

internal sealed class MasterDataReader(MasterDataDbContext context, MachineRegister machines, IOptions<PlantOptions> plantOptions) : IMasterDataReader
{
    // Checked at startup (AC-41), so a missing value here is a bug.
    public string PlantTimeZoneId =>
        plantOptions.Value.TimeZone ?? throw new InvalidOperationException("The plant time zone setting is missing although startup checks it.");

    public async Task<ProductRouting?> FindProductRoutingAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await context.Products.AnyAsync(product => product.Id == productId, cancellationToken))
        {
            return null;
        }

        List<RoutingStageValues> stages = await context.RoutingStages
            .Where(stage => stage.ProductId == productId)
            .OrderBy(stage => stage.SequenceNo)
            .Select(stage => new RoutingStageValues
            {
                SequenceNo = stage.SequenceNo,
                ResourceTypeId = stage.ResourceTypeId,
                SetupMinutes = stage.SetupMinutes,
                CycleMinutes = stage.CycleMinutes,
                UnitsPerCycle = stage.UnitsPerCycle,
            })
            .ToListAsync(cancellationToken);
        List<RoutingLinkValues> dependencies = await context.RoutingDependencies
            .Where(dependency => dependency.ProductId == productId)
            .OrderBy(dependency => dependency.SuccessorStage!.SequenceNo)
            .ThenBy(dependency => dependency.PredecessorStage!.SequenceNo)
            .Select(dependency => new RoutingLinkValues
            {
                PredecessorSequenceNo = dependency.PredecessorStage!.SequenceNo,
                SuccessorSequenceNo = dependency.SuccessorStage!.SequenceNo,
                MinLagMinutes = dependency.MinLagMinutes,
            })
            .ToListAsync(cancellationToken);
        return new ProductRouting { Stages = stages, Dependencies = dependencies };
    }

    public async Task<IReadOnlyList<int>> FindProductIdsContainingAsync(string text, CancellationToken cancellationToken)
    {
        string pattern = ContainsPattern.For(text);
        return await context.Products
            .Where(product => EF.Functions.ILike(product.Code, pattern, ContainsPattern.EscapeCharacter)
                || EF.Functions.ILike(product.Name, pattern, ContainsPattern.EscapeCharacter))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, ProductReference>> GetProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken) =>
        await context.Products
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new ProductReference { Id = product.Id, Code = product.Code, Name = product.Name })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

    public async Task<IReadOnlyDictionary<int, NamedReference>> GetResourceTypesAsync(IReadOnlyCollection<int> resourceTypeIds, CancellationToken cancellationToken) =>
        await context.ResourceTypes
            .Where(type => resourceTypeIds.Contains(type.Id))
            .Select(type => new NamedReference { Id = type.Id, Name = type.Name })
            .ToDictionaryAsync(type => type.Id, cancellationToken);

    public Task<IReadOnlyList<ResourceResponse>> ListResourcesAsync(int? resourceTypeId, bool? isActive, CancellationToken cancellationToken) =>
        machines.ListAsync(resourceTypeId, isActive, cancellationToken);

    public Task<ResourceResponse> GetResourceAsync(int resourceId, CancellationToken cancellationToken) =>
        machines.GetAsync(resourceId, cancellationToken);

    public Task<bool> ResourceExistsAsync(int resourceId, CancellationToken cancellationToken) =>
        context.Resources.AnyAsync(resource => resource.Id == resourceId, cancellationToken);

    public async Task<PlantWorkingTime> GetWorkingTimeAsync(CancellationToken cancellationToken)
    {
        List<CalendarWorkingTime> calendars = await context.Calendars
            .Select(calendar => new CalendarWorkingTime
            {
                Id = calendar.Id,
                Shifts = calendar.Shifts
                    .Select(shift => new ShiftWorkingTime
                    {
                        Weekday = shift.Weekday,
                        StartMinuteOfDay = shift.StartMinuteOfDay,
                        EndMinuteOfDay = shift.EndMinuteOfDay,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        List<DateOnly> holidays = await context.Holidays.Select(holiday => holiday.Date).ToListAsync(cancellationToken);
        return new PlantWorkingTime { Calendars = calendars, Holidays = holidays };
    }
}
