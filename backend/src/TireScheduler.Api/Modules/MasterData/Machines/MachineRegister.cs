using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Machines;

// F-MD-1. A machine is a resource (DEC-7).
internal sealed class MachineRegister(
    MasterDataDbContext context,
    IEnumerable<IResourceReferences> resourceReferences,
    IResourcePlacementReferences placementReferences,
    RequestTransaction requestTransaction)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.ResourceNameIndex] = "name" };

    // By type, then name (AC-30).
    public async Task<IReadOnlyList<ResourceResponse>> ListAsync(int? resourceTypeId, bool? isActive, CancellationToken cancellationToken) =>
        await Responses(context.Resources
                .Where(resource => resourceTypeId == null || resource.ResourceTypeId == resourceTypeId)
                .Where(resource => isActive == null || resource.IsActive == isActive)
                .OrderBy(resource => resource.ResourceType!.Name)
                .ThenBy(resource => resource.Name))
            .ToListAsync(cancellationToken);

    public async Task<ResourceResponse> GetAsync(int id, CancellationToken cancellationToken) =>
        await Responses(context.Resources.Where(resource => resource.Id == id)).FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFound();

    public async Task<ResourceResponse> CreateAsync(CreateResourceRequest request, CancellationToken cancellationToken)
    {
        int resourceTypeId = request.ResourceTypeId.Validated();
        await RequireResourceTypeAsync(resourceTypeId, cancellationToken);
        // v1 has one calendar, so every machine follows the plant default (A3).
        int plantDefaultCalendarId = await context.Calendars
            .Where(calendar => calendar.IsPlantDefault)
            .Select(calendar => calendar.Id)
            .SingleAsync(cancellationToken);

        Resource resource = new() { Name = request.Name.Trim(), ResourceTypeId = resourceTypeId, CalendarId = plantDefaultCalendarId };
        context.Resources.Add(resource);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        return await GetAsync(resource.Id, cancellationToken);
    }

    // Locks the row first, so a lot cannot be placed on the resource while its type is checked and
    // changed (DEC-33). Deactivating is always saved; its placements become R1 conflicts (A15).
    public async Task<ResourceResponse> UpdateAsync(int id, UpdateResourceRequest request, CancellationToken cancellationToken)
    {
        int resourceTypeId = request.ResourceTypeId.Validated();
        await using (OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken))
        {
            if (!await context.LockRowAsync<Resource>(id, cancellationToken))
            {
                throw NotFound();
            }

            Resource resource = await FindAsync(id, cancellationToken);
            await RequireResourceTypeAsync(resourceTypeId, cancellationToken);
            if (resourceTypeId != resource.ResourceTypeId
                && await placementReferences.FindPlacementOnResourceAsync(id, cancellationToken) is { } placement)
            {
                throw ApiProblemException.InUse(
                    $"Machine '{resource.Name}' cannot change its type because {placement} is on it (A14). Move or remove its placements first.");
            }

            resource.Name = request.Name.Trim();
            resource.ResourceTypeId = resourceTypeId;
            resource.IsActive = request.IsActive.Validated();
            await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    // Locks the row first, so an unavailability cannot be recorded on the resource while it is
    // checked and deleted (DEC-33).
    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        if (!await context.LockRowAsync<Resource>(id, cancellationToken))
        {
            throw NotFound();
        }

        Resource resource = await FindAsync(id, cancellationToken);
        foreach (IResourceReferences references in resourceReferences)
        {
            if (await references.FindItemUsingResourceAsync(id, cancellationToken) is { } itemUsingResource)
            {
                throw ApiProblemException.InUse(
                    $"Machine '{resource.Name}' cannot be deleted because {itemUsingResource} refers to it. Deactivate the machine instead (A14).");
            }
        }

        context.Resources.Remove(resource);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Resource> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Resources.FirstOrDefaultAsync(resource => resource.Id == id, cancellationToken)
        ?? throw NotFound();

    private async Task RequireResourceTypeAsync(int resourceTypeId, CancellationToken cancellationToken)
    {
        if (!await context.ResourceTypes.AnyAsync(type => type.Id == resourceTypeId, cancellationToken))
        {
            throw ApiProblemException.ValidationFailed("resourceTypeId", "This machine type does not exist.");
        }
    }

    private static IQueryable<ResourceResponse> Responses(IQueryable<Resource> resources) =>
        resources.Select(resource => new ResourceResponse
        {
            Id = resource.Id,
            Name = resource.Name,
            ResourceType = new NamedReference { Id = resource.ResourceType!.Id, Name = resource.ResourceType.Name },
            Calendar = new NamedReference { Id = resource.Calendar!.Id, Name = resource.Calendar.Name },
            IsActive = resource.IsActive,
        });

    private static ApiProblemException NotFound() => ApiProblemException.NotFound("This machine");
}
