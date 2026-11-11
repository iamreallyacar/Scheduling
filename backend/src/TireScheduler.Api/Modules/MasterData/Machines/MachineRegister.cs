using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Machines;

// F-MD-1. A machine is a resource (DEC-7).
internal sealed class MachineRegister(MasterDataDbContext context)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.ResourceNameIndex] = "name" };

    // By type, then name (AC-30).
    public async Task<IReadOnlyList<ResourceResponse>> ListAsync(int? resourceTypeId, bool? isActive, CancellationToken cancellationToken) =>
        await Responses(context.Resources
                .Where(resource => resourceTypeId == null || resource.ResourceTypeId == resourceTypeId)
                .Where(resource => isActive == null || resource.IsActive == isActive)
                .OrderBy(resource => resource.ResourceType!.Name)
                .ThenBy(resource => resource.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ResourceResponse> GetAsync(int id, CancellationToken cancellationToken) =>
        await Responses(context.Resources.Where(resource => resource.Id == id)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
        ?? throw NotFound();

    public async Task<ResourceResponse> CreateAsync(CreateResourceRequest request, CancellationToken cancellationToken)
    {
        int resourceTypeId = request.ResourceTypeId.Validated();
        await RequireResourceTypeAsync(resourceTypeId, cancellationToken).ConfigureAwait(false);
        // v1 has one calendar, so every machine follows the plant default (A3).
        int plantDefaultCalendarId = await context.Calendars
            .Where(calendar => calendar.IsPlantDefault)
            .Select(calendar => calendar.Id)
            .SingleAsync(cancellationToken).ConfigureAwait(false);

        Resource resource = new() { Name = request.Name.Trim(), ResourceTypeId = resourceTypeId, CalendarId = plantDefaultCalendarId };
        context.Resources.Add(resource);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return await GetAsync(resource.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourceResponse> UpdateAsync(int id, UpdateResourceRequest request, CancellationToken cancellationToken)
    {
        Resource resource = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        int resourceTypeId = request.ResourceTypeId.Validated();
        await RequireResourceTypeAsync(resourceTypeId, cancellationToken).ConfigureAwait(false);

        resource.Name = request.Name.Trim();
        resource.ResourceTypeId = resourceTypeId;
        resource.IsActive = request.IsActive.Validated();
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Resource resource = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        context.Resources.Remove(resource);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Resource> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Resources.FirstOrDefaultAsync(resource => resource.Id == id, cancellationToken).ConfigureAwait(false)
        ?? throw NotFound();

    private async Task RequireResourceTypeAsync(int resourceTypeId, CancellationToken cancellationToken)
    {
        if (!await context.ResourceTypes.AnyAsync(type => type.Id == resourceTypeId, cancellationToken).ConfigureAwait(false))
        {
            throw ApiProblemException.ValidationFailed("resourceTypeId", "This machine type does not exist.");
        }
    }

    private static IQueryable<ResourceResponse> Responses(IQueryable<Resource> resources) =>
        resources.Select(resource => new ResourceResponse(
            resource.Id,
            resource.Name,
            new NamedReference(resource.ResourceType!.Id, resource.ResourceType.Name),
            new NamedReference(resource.Calendar!.Id, resource.Calendar.Name),
            resource.IsActive));

    private static ApiProblemException NotFound() => ApiProblemException.NotFound("This machine");
}
