namespace TireScheduler.Api.Modules.MasterData;

// Row locks of MasterData for the commands of the modules above it (DEC-33). Each method joins the
// command's open transaction and holds the row until it ends.
public interface IMasterDataLocks
{
    // False when the resource does not exist.
    Task<bool> LockResourceAsync(int resourceId, CancellationToken cancellationToken);
}
