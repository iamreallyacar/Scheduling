using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Machines;

internal sealed class ResourceTypeRegister(MasterDataDbContext context, IEnumerable<IResourceTypeReferences> resourceTypeReferences)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.ResourceTypeNameIndex] = "name" };

    public async Task<IReadOnlyList<ResourceTypeResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.ResourceTypes
            .OrderBy(type => type.Name)
            .Select(type => new ResourceTypeResponse { Id = type.Id, Name = type.Name, Kind = type.Kind })
            .ToListAsync(cancellationToken);

    public async Task<ResourceTypeResponse> CreateAsync(ResourceTypeRequest request, CancellationToken cancellationToken)
    {
        ResourceType resourceType = new() { Name = request.Name.Trim(), Kind = ParseKind(request.Kind) };
        context.ResourceTypes.Add(resourceType);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        return ToResponse(resourceType);
    }

    public async Task<ResourceTypeResponse> UpdateAsync(int id, ResourceTypeRequest request, CancellationToken cancellationToken)
    {
        ResourceType resourceType = await FindAsync(id, cancellationToken);
        resourceType.Name = request.Name.Trim();
        resourceType.Kind = ParseKind(request.Kind);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        return ToResponse(resourceType);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        ResourceType resourceType = await FindAsync(id, cancellationToken);
        string? machineOfThisType = await context.Resources
            .Where(resource => resource.ResourceTypeId == id)
            .OrderBy(resource => resource.Name)
            .Select(resource => resource.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (machineOfThisType is not null)
        {
            throw ApiProblemException.InUse($"Machine type '{resourceType.Name}' cannot be deleted because machine '{machineOfThisType}' has this type.");
        }

        string? productRoutedOverThisType = await context.RoutingStages
            .Where(stage => stage.ResourceTypeId == id)
            .Join(context.Products, stage => stage.ProductId, product => product.Id, (stage, product) => product.Code)
            .OrderBy(productCode => productCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (productRoutedOverThisType is not null)
        {
            throw ApiProblemException.InUse($"Machine type '{resourceType.Name}' cannot be deleted because the routing of product '{productRoutedOverThisType}' uses it.");
        }

        foreach (IResourceTypeReferences references in resourceTypeReferences)
        {
            if (await references.FindItemUsingResourceTypeAsync(id, cancellationToken) is { } itemUsingType)
            {
                throw ApiProblemException.InUse($"Machine type '{resourceType.Name}' cannot be deleted because {itemUsingType} uses it.");
            }
        }

        context.ResourceTypes.Remove(resourceType);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ResourceType> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.ResourceTypes.FirstOrDefaultAsync(type => type.Id == id, cancellationToken)
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

    private static ResourceTypeResponse ToResponse(ResourceType resourceType) => new()
    {
        Id = resourceType.Id,
        Name = resourceType.Name,
        Kind = resourceType.Kind,
    };
}
