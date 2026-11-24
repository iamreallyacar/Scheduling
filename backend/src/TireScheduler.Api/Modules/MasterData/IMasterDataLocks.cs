namespace TireScheduler.Api.Modules.MasterData;

// Row locks of MasterData for the commands of the modules above it (DEC-33). Each method joins the
// command's open transaction and holds the row until it ends.
public interface IMasterDataLocks
{
    // False when the resource does not exist.
    Task<bool> LockResourceAsync(int resourceId, CancellationToken cancellationToken);

    // In ascending id order, so two commands that take them all never wait on each other. A
    // command that also locks an order takes them before it (DEC-44, DEC-67).
    Task LockEveryResourceAsync(CancellationToken cancellationToken);

    // False when the product does not exist. Held, the product cannot be deleted and its routing
    // cannot be replaced (DEC-66).
    Task<bool> LockProductAsync(int productId, CancellationToken cancellationToken);
}
