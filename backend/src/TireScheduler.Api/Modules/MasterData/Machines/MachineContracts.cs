using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.MasterData.Machines;

public sealed record ResourceTypeRequest
{
    [Required]
    [StringLength(ResourceType.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    // Optional; a string and not ResourceKind, so an unknown kind is a field error (AC-27)
    // and not an unreadable request.
    public string? Kind { get; init; }
}

public sealed record ResourceTypeResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required ResourceKind Kind { get; init; }
}

public sealed record CreateResourceRequest
{
    [Required]
    [StringLength(Resource.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public int? ResourceTypeId { get; init; }
}

public sealed record UpdateResourceRequest
{
    [Required]
    [StringLength(Resource.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public int? ResourceTypeId { get; init; }

    [Required]
    public bool? IsActive { get; init; }
}

public sealed record ResourceResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required NamedReference Calendar { get; init; }

    public required bool IsActive { get; init; }
}
