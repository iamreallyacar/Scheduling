namespace TireScheduler.Api.Modules.MasterData;

// Ports (DEC-16): a module above MasterData that keeps ids of master data names the first of its
// rows that uses an item, so the item is not deleted while in use (A14, AC-20). The answer is
// the name the user knows that row by, for example "order 'SO-1'", or null when nothing uses it.
public interface IProductReferences
{
    Task<string?> FindItemUsingProductAsync(int productId, CancellationToken cancellationToken);
}

public interface IResourceTypeReferences
{
    Task<string?> FindItemUsingResourceTypeAsync(int resourceTypeId, CancellationToken cancellationToken);
}
