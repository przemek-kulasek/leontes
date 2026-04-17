namespace Leontes.Domain.Entities;

public sealed class SynapseRelationship : Entity
{
    public required Guid SourceEntityId { get; set; }
    public required Guid TargetEntityId { get; set; }
    public required string RelationType { get; set; }
    public float Weight { get; set; } = 1.0f;
    public string? Context { get; set; }

    public SynapseEntity SourceEntity { get; init; } = null!;
    public SynapseEntity TargetEntity { get; init; } = null!;
}
