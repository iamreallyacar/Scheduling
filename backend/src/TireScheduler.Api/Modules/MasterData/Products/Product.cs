namespace TireScheduler.Api.Modules.MasterData.Products;

internal sealed class Product
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 200;
    public const int SpecMaxLength = 500;

    public int Id { get; private set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Spec { get; set; }
}
