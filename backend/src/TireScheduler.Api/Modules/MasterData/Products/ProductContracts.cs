using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.MasterData.Products;

public sealed record ProductRequest
{
    [Required]
    [StringLength(Product.CodeMaxLength)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(Product.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [StringLength(Product.SpecMaxLength)]
    public string? Spec { get; init; }
}

public sealed record ProductResponse
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string? Spec { get; init; }
}
