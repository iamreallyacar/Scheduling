using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Machines;

internal sealed class ResourceTypeRegister(MasterDataDbContext context)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.ResourceTypeNameIndex] = "name" };

    public async Task<IReadOnlyList<ResourceTypeResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.ResourceTypes
            .OrderBy(type => type.Name)
            .Select(type => new ResourceTypeResponse(type.Id, type.Name, type.Kind))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ResourceTypeResponse> CreateAsync(ResourceTypeRequest request, CancellationToken cancellationToken)
    {
        ResourceType resourceType = new() { Name = request.Name.Trim(), Kind = ParseKind(request.Kind) };
        context.ResourceTypes.Add(resourceType);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return ToResponse(resourceType);
    }

    public async Task<ResourceTypeResponse> UpdateAsync(int id, ResourceTypeRequest request, CancellationToken cancellationToken)
    {
        ResourceType resourceType = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        resourceType.Name = request.Name.Trim();
        resourceType.Kind = ParseKind(request.Kind);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return ToResponse(resourceType);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        ResourceType resourceType = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        string? machineOfThisType = await context.Resources
            .Where(resource => resource.ResourceTypeId == id)
            .OrderBy(resource => resource.Name)
            .Select(resource => resource.Name)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (machineOfThisType is not null)
        {
            throw ApiProblemException.InUse($"Machine type '{resourceType.Name}' cannot be deleted because machine '{machineOfThisType}' has this type.");
        }

        context.ResourceTypes.Remove(resourceType);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResourceType> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.ResourceTypes.FirstOrDefaultAsync(type => type.Id == id, cancellationToken).ConfigureAwait(false)
        ?? throw ApiProblemException.NotFound("This machine type");

    private static ResourceKind ParseKind(string? kind)
    {
        if (kind is null)
        {
            return ResourceKind.Machine;
        }

        return Enum.TryParse(kind, ignoreCase: false, out ResourceKind parsedKind) && Enum.IsDefined(parsedKind)
            ? parsedKind
            : throw ApiProblemException.ValidationFailed("kind", $"The kind must be one of: {string.Join(", ", Enum.GetNames<ResourceKind>())}.");
    }

    private static ResourceTypeResponse ToResponse(ResourceType resourceType) => new(resourceType.Id, resourceType.Name, resourceType.Kind);
}
