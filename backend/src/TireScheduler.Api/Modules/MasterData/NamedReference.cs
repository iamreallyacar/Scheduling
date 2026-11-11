namespace TireScheduler.Api.Modules.MasterData;

// A row another row points to, with the name the UI shows for it.
public sealed record NamedReference(int Id, string Name);
