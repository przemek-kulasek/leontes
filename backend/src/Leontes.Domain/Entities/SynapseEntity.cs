using Leontes.Domain.Enums;
using Pgvector;

namespace Leontes.Domain.Entities;

public sealed class SynapseEntity : Entity
{
    public required string Name { get; set; }
    public required SynapseEntityType EntityType { get; set; }
    public string? Description { get; set; }
    public Vector? Embedding { get; set; }
    public Dictionary<string, string> Properties { get; set; } = [];
}
