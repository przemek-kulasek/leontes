# 80 — Hierarchical Memory

## Problem

The assistant currently has no memory beyond the active conversation. Each session starts from zero — it forgets preferences, past decisions, people, and context. Standard flat vector search (RAG) retrieves similar text but misses relational context ("Sarah works on Project X" or "last time we discussed the budget, the conclusion was Y"). A human-like memory requires multiple memory types working together, backed by graph-augmented retrieval.

## Prerequisites

- Working CLI chat (feature 10)
- Thinking Pipeline implemented (feature 70) — memory is consumed in the Enrich stage and written in the Reflect stage

## Rules

- All memory storage uses PostgreSQL 17 — no separate vector or graph databases
- Vector embeddings use `Pgvector.EntityFrameworkCore` (approved, version-pinned in Directory.Packages.props)
- Graph-like queries use recursive CTEs and standard FK relationships — no Neo4j, no Kuzu
- Embedding model must be configurable (local via Ollama or cloud API) — provider-agnostic in Application layer
- Memory retrieval must not add more than 500ms latency to the pipeline Enrich stage under normal load
- Consolidation runs as a background process, never blocking the request pipeline
- Feature 20 (Long-Term Memory) is superseded by this spec — this is the complete memory architecture

## Background

### Memory Types (Neuroscience Mapping)

| Memory Type | Brain Region | Purpose | Implementation |
|---|---|---|---|
| Working Memory | Prefrontal Cortex | Current conversation context | Context window — last N messages from the active conversation |
| Episodic Memory | Hippocampus | Specific events and experiences | Vector-indexed entries from past conversations ("yesterday we discussed Q3 budget") |
| Semantic Memory | Temporal Lobe | Facts and relationships | Synapse Graph — entities and relationships (`(Sarah)-[WORKS_ON]->(Project X)`) |
| Procedural Memory | Cerebellum | Learned skills and patterns | Tool Forge skill catalog (feature 115) — stored tool definitions and usage patterns |

### Graph-Augmented Retrieval (GraphRAG)

Standard RAG: "Send it to Sarah" → vector search for "Sarah" → maybe finds a message mentioning Sarah.

GraphRAG: "Send it to Sarah" → graph query for Sarah → finds email, phone, Signal number → finds recent files linked to her → finds her role and team → enriches the LLM prompt with structured context.

The difference: vector search finds *similar text*, graph search finds *connected knowledge*.

### Memory Consolidation (Generative Agents, Park et al. 2023)

Raw observations accumulate quickly and become noise. The brain consolidates memories during sleep — distilling specific events into general insights. The assistant needs a similar process:

- Raw: "User asked about PostgreSQL migration at 2pm" + "User asked about EF Core migration at 3pm" + "User asked about database versioning at 4pm"
- Consolidated: "User is working on a database migration strategy — likely planning a schema change"

This consolidation runs as a periodic background task, producing higher-level `MemoryEntry` records tagged as insights.

## Solution

### Architecture Overview

```
User Message
    |
    v
[Enrich Stage] ──────────────────────────────────┐
    |                                              |
    ├── Working Memory: load last N messages       |
    ├── Episodic Memory: vector search pgvector    |
    ├── Semantic Memory: graph query (CTEs)        |
    └── Procedural Memory: tool catalog lookup     |
    |                                              |
    v                                              |
[LLM with enriched context]                        |
    |                                              |
    v                                              |
[Reflect Stage] ──────────────────────────────────┘
    |
    ├── Store new episodic memories (embed + save)
    ├── Extract entities and relationships → Synapse Graph
    └── Queue observations for consolidation
    |
    v
[Consolidation Service] (background, periodic)
    |
    ├── Batch recent observations
    ├── LLM: distill into insights
    └── Store consolidated memories (higher-level)
```

### Data Model

#### MemoryEntry (Domain Layer)

```csharp
public sealed class MemoryEntry : Entity
{
    public required string Content { get; set; }
    public required Vector Embedding { get; set; }
    public MemoryType Type { get; set; }
    public Guid? SourceMessageId { get; set; }
    public Guid? SourceConversationId { get; set; }
    public float Importance { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
}

public enum MemoryType
{
    Observation,
    Insight,
    Preference,
    Fact
}
```

#### SynapseEntity (Domain Layer)

```csharp
public sealed class SynapseEntity : Entity
{
    public required string Name { get; set; }
    public required SynapseEntityType EntityType { get; set; }
    public string? Description { get; set; }
    public Vector? Embedding { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public enum SynapseEntityType
{
    Person,
    Project,
    File,
    Tool,
    Concept
}
```

#### SynapseRelationship (Domain Layer)

```csharp
public sealed class SynapseRelationship : Entity
{
    public required Guid SourceEntityId { get; set; }
    public required Guid TargetEntityId { get; set; }
    public required string RelationType { get; set; }
    public float Weight { get; set; } = 1.0f;
    public string? Context { get; set; }

    public SynapseEntity SourceEntity { get; set; } = null!;
    public SynapseEntity TargetEntity { get; set; } = null!;
}
```

### Components

#### 1. IEmbeddingService (Application Layer)

```csharp
public interface IEmbeddingService
{
    Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken);
    Task<IReadOnlyList<Vector>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}
```

Implementation in Infrastructure uses `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>` — provider-agnostic. Wired to Ollama (local) or cloud via configuration.

#### 2. IMemoryStore (Application Layer)

```csharp
public interface IMemoryStore
{
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        string content,
        MemoryType type,
        Guid? sourceMessageId = null,
        Guid? sourceConversationId = null,
        float importance = 0.5f,
        CancellationToken cancellationToken = default);
}

public sealed record MemorySearchResult(
    Guid Id,
    string Content,
    MemoryType Type,
    float Relevance,
    DateTime CreatedAt);
```

Implementation uses pgvector cosine distance for similarity search:

```sql
SELECT id, content, type, created,
       1 - (embedding <=> @queryEmbedding) AS relevance
FROM memory_entries
ORDER BY embedding <=> @queryEmbedding
LIMIT @limit;
```

#### 3. ISynapseGraph (Application Layer)

```csharp
public interface ISynapseGraph
{
    Task<SynapseEntity?> FindEntityAsync(
        string name,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SynapseEntity>> FindRelatedEntitiesAsync(
        Guid entityId,
        int depth = 2,
        CancellationToken cancellationToken = default);

    Task<SynapseEntity> UpsertEntityAsync(
        string name,
        SynapseEntityType type,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    Task AddRelationshipAsync(
        Guid sourceId,
        Guid targetId,
        string relationType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynapseEntity>> SemanticSearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
```

Graph traversal uses recursive CTEs:

```sql
WITH RECURSIVE graph AS (
    -- Base: start from the target entity
    SELECT e.id, e.name, e.entity_type, 0 AS depth
    FROM synapse_entities e
    WHERE e.id = @startEntityId

    UNION ALL

    -- Recurse: follow relationships
    SELECT e.id, e.name, e.entity_type, g.depth + 1
    FROM synapse_entities e
    INNER JOIN synapse_relationships r
        ON (r.target_entity_id = e.id OR r.source_entity_id = e.id)
    INNER JOIN graph g
        ON (r.source_entity_id = g.id OR r.target_entity_id = g.id)
    WHERE g.depth < @maxDepth
      AND e.id != g.id
)
SELECT DISTINCT id, name, entity_type, depth
FROM graph
ORDER BY depth;
```

#### 4. MemoryConsolidationService (Infrastructure Layer)

Background service (`IHostedService`) that runs periodically:

```csharp
public sealed class MemoryConsolidationService(
    IMemoryStore memoryStore,
    IChatClient chatClient,
    ILogger<MemoryConsolidationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            await ConsolidateAsync(stoppingToken);
        }
    }
}
```

Consolidation process:
1. Fetch recent `Observation` memories not yet consolidated (last hour)
2. Group by topic/conversation
3. Send to LLM: "Distill these observations into 1-3 key insights"
4. Store insights as `MemoryType.Insight` with higher importance
5. Do not delete the original observations — they remain searchable

### API Contract

```http
### Search memories
GET /api/v1/memory/search?q=Sarah&limit=5
Authorization: Bearer {token}

### Response
[
    {
        "id": "guid",
        "content": "Sarah is the lead developer on Project Alpha",
        "type": "Fact",
        "relevance": 0.92,
        "createdAt": "2026-04-10T14:30:00Z"
    }
]
```

```http
### Browse Synapse Graph entity
GET /api/v1/synapse/entities/{entityId}/related?depth=2
Authorization: Bearer {token}

### Response
{
    "entity": {
        "id": "guid",
        "name": "Sarah",
        "entityType": "Person",
        "properties": { "email": "sarah@example.com", "role": "Lead Developer" }
    },
    "related": [
        {
            "id": "guid",
            "name": "Project Alpha",
            "entityType": "Project",
            "relationType": "WORKS_ON",
            "depth": 1
        }
    ]
}
```

### Pipeline Integration

**Enrich Stage** (from feature 70) queries all memory types:

```csharp
public sealed class EnrichStage(
    IMemoryStore memoryStore,
    ISynapseGraph synapseGraph) : IPipelineStage
{
    public string Name => "Enrich";
    public int Order => 200;

    public async Task ExecuteAsync(ThinkingContext context, CancellationToken cancellationToken)
    {
        // Episodic: vector search for relevant past experiences
        context.RelevantMemories = await memoryStore.SearchAsync(
            context.UserMessage.Content, limit: 5, cancellationToken);

        // Semantic: resolve entities mentioned in the message
        foreach (var entity in context.ExtractedEntities)
        {
            var synapseEntity = await synapseGraph.FindEntityAsync(entity, cancellationToken);
            if (synapseEntity is not null)
            {
                var related = await synapseGraph.FindRelatedEntitiesAsync(
                    synapseEntity.Id, depth: 2, cancellationToken);
                // Add to context for LLM prompt
            }
        }
    }
}
```

**Reflect Stage** stores new memories:

```csharp
public sealed class ReflectStage(
    IMemoryStore memoryStore,
    ISynapseGraph synapseGraph) : IPipelineStage
{
    public string Name => "Reflect";
    public int Order => 500;

    public async Task ExecuteAsync(ThinkingContext context, CancellationToken cancellationToken)
    {
        if (!context.IsComplete) return; // Skip on interrupted responses

        // Store the exchange as an observation
        await memoryStore.StoreAsync(
            $"User asked: {context.UserMessage.Content}\nAssistant answered: {context.Response}",
            MemoryType.Observation,
            context.UserMessage.Id,
            context.Conversation.Id,
            cancellationToken: cancellationToken);

        // Extract and upsert entities mentioned in the conversation
        // (entity extraction happens in Perceive stage)
    }
}
```

### EF Core Configuration

pgvector extension must be enabled in ApplicationDbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasPostgresExtension("vector");

    modelBuilder.Entity<MemoryEntry>(entity =>
    {
        entity.Property(e => e.Embedding)
            .HasColumnType("vector(384)"); // dimension depends on embedding model

        entity.HasIndex(e => e.Embedding)
            .HasMethod("ivfflat")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("lists", 100);
    });

    modelBuilder.Entity<SynapseEntity>(entity =>
    {
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.EntityType);

        entity.Property(e => e.Properties)
            .HasColumnType("jsonb");

        entity.Property(e => e.Embedding)
            .HasColumnType("vector(384)");
    });

    modelBuilder.Entity<SynapseRelationship>(entity =>
    {
        entity.HasOne(e => e.SourceEntity)
            .WithMany()
            .HasForeignKey(e => e.SourceEntityId);

        entity.HasOne(e => e.TargetEntity)
            .WithMany()
            .HasForeignKey(e => e.TargetEntityId);

        entity.HasIndex(e => new { e.SourceEntityId, e.TargetEntityId, e.RelationType })
            .IsUnique();
    });
}
```

### Configuration

```json
{
    "Memory": {
        "EmbeddingDimensions": 384,
        "ConsolidationIntervalHours": 1,
        "MaxRetrievalResults": 5,
        "MinRelevanceThreshold": 0.7
    }
}
```

Embedding model configured through the existing `AiProvider` settings — the `IEmbeddingService` implementation uses `Microsoft.Extensions.AI.IEmbeddingGenerator` which is provider-agnostic.

### Migration

```bash
dotnet ef migrations add AddHierarchicalMemory \
    --project backend/src/Leontes.Infrastructure \
    --startup-project backend/src/Leontes.Api
```

This migration:
1. Enables the `vector` PostgreSQL extension
2. Creates `memory_entries` table with vector column and IVFFlat index
3. Creates `synapse_entities` table with JSONB properties and optional vector column
4. Creates `synapse_relationships` table with composite unique index

## Acceptance Criteria

- [ ] `MemoryEntry` entity with pgvector embedding column exists and is indexed
- [ ] `SynapseEntity` and `SynapseRelationship` entities model the knowledge graph
- [ ] `IMemoryStore.SearchAsync` returns relevant memories using cosine similarity
- [ ] `ISynapseGraph.FindRelatedEntitiesAsync` traverses relationships using recursive CTEs
- [ ] `IEmbeddingService` is provider-agnostic and works with Ollama locally
- [ ] Enrich stage queries all memory types and injects context into the LLM prompt
- [ ] Reflect stage stores new observations after each completed conversation turn
- [ ] Memory consolidation runs as a background service, distilling observations into insights
- [ ] `GET /api/v1/memory/search` endpoint returns relevant memories with relevance scores
- [ ] `GET /api/v1/synapse/entities/{id}/related` endpoint returns graph-connected entities
- [ ] User can ask "what do you remember about X" and get accurate recall
- [ ] Memory retrieval adds less than 500ms latency to the Enrich stage

## Open Questions

- Embedding model choice for local dev: `all-MiniLM-L6-v2` (384 dimensions) or `nomic-embed-text` (768 dimensions)?
- Chunking strategy: embed entire conversation turns or split into sentences?
- Memory decay: should old, never-accessed memories lose importance over time?

## Out of Scope

- Separate vector database (Qdrant, Milvus, Pinecone) — pgvector handles this at our scale
- Separate graph database (Neo4j, Kuzu) — PostgreSQL recursive CTEs are sufficient
- User-facing memory management UI (edit/delete memories)
- Cross-user memory isolation (single-user deployment)
- Real-time memory streaming (memories are queried per-request, not pushed)
