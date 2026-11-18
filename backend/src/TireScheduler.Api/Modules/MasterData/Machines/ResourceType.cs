namespace TireScheduler.Api.Modules.MasterData.Machines;

internal sealed class ResourceType
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }

    public required string Name { get; set; }

    public ResourceKind Kind { get; set; }
}
