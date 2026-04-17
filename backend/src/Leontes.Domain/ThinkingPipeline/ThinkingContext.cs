using Leontes.Domain.Enums;

namespace Leontes.Domain.ThinkingPipeline;

/// <summary>
/// Shared context that flows through all Thinking Pipeline stages.
/// JSON-serializable for workflow checkpointing.
/// </summary>
public sealed class ThinkingContext
{
    public required Guid MessageId { get; init; }
    public required Guid ConversationId { get; init; }
    public required string UserContent { get; init; }
    public required string Channel { get; init; }

    // Populated by Perceive
    public string? Intent { get; set; }
    public IReadOnlyList<string> ExtractedEntities { get; set; } = [];
    public MessageUrgency Urgency { get; set; } = MessageUrgency.Normal;

    // Populated by Enrich
    public IReadOnlyList<RelevantMemory> RelevantMemories { get; set; } = [];
    public IReadOnlyList<HistoryMessage> ConversationHistory { get; set; } = [];
    public IReadOnlyList<ResolvedEntity> ResolvedEntities { get; set; } = [];

    // Populated by Plan
    public string? Plan { get; set; }
    public IReadOnlyList<string> SelectedTools { get; set; } = [];
    public bool RequiresHumanInput { get; set; }
    public string? HumanInputQuestion { get; set; }
    public string? HumanInputResponse { get; set; }

    // Populated by Execute
    public string? Response { get; set; }
    public bool IsComplete { get; set; }
    public IReadOnlyList<ToolCallResult> ToolResults { get; set; } = [];

    // Populated by Reflect
    public IReadOnlyList<string> NewInsights { get; set; } = [];
    public IReadOnlyList<EntityUpdate> GraphUpdates { get; set; } = [];
}

public sealed record RelevantMemory(
    Guid MemoryId, string Content, MemoryType Type, double Relevance, DateTime CreatedAt);

public sealed record HistoryMessage(
    string Role, string Content, DateTime Timestamp);

public sealed record ResolvedEntity(
    string Mention, Guid EntityId, string EntityType, string ResolvedName);

public sealed record ToolCallResult(
    string ToolName, string Input, string Output, bool Success);

public sealed record EntityUpdate(
    Guid EntityId, string RelationType, Guid RelatedEntityId);
