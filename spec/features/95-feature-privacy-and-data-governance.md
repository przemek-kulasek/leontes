# 95 — Privacy & Data Governance

## Problem

Leontes monitors clipboard content, active windows, calendar entries, and file system changes. It stores conversation history, builds a knowledge graph of people and projects, and generates executable code at runtime. All of this happens on the user's personal machine with access to sensitive data — yet no spec defines what data the agent collects, how long it keeps it, or how the user controls it.

Even in a single-user, local-only deployment, privacy matters. The user may share their screen during a meeting, let someone else use their computer briefly, or simply want to know what their assistant "knows" about them. Without explicit data governance, the agent becomes an opaque surveillance system rather than a trusted partner.

## Prerequisites

- Working feature 55 (Proactive Communication — for consent prompts)
- Working feature 70 (Hierarchical Memory — the primary data store to govern)
- Working feature 80 (Sentinel Intelligence — the primary data collector to govern)

## Rules

- Sentinel monitors are opt-in, not opt-out — each must be explicitly enabled during setup (feature 30) or via settings
- No data collection starts before explicit user consent during first run
- The user can review, search, and delete any data the agent has stored — no hidden state
- Clipboard content and file contents are never stored in plaintext in the database — store hashed fingerprints or redacted summaries
- Window titles from applications on the privacy exclusion list (feature 90) are never logged, even as metadata
- Data retention limits are enforced automatically — expired data is hard-deleted, not soft-deleted
- Export format must be human-readable (JSON + Markdown) and machine-parseable
- All privacy settings are stored in the database and can be changed at any time — not baked into configuration files

## Background

### The Trust Equation

A proactive assistant is only useful if the user trusts it. Trust = Transparency + Control + Predictability.

- **Transparency:** The user can see exactly what data the agent has collected
- **Control:** The user can delete data, disable monitors, and set retention limits
- **Predictability:** The agent behaves consistently — the same privacy settings produce the same behavior

### Data Categories and Sensitivity

Not all data is equal. Some is transient and harmless (e.g., "user switched to VS Code"), while other data is highly sensitive (e.g., clipboard content that might contain passwords or bank details).

| Category | Source | Sensitivity | Default Retention |
|---|---|---|---|
| Conversation messages | CLI, Signal, Telegram | Medium | Indefinite (user data) |
| Conversation summaries | Context Manager (feature 75) | Low | Indefinite |
| Episodic memories | Reflect stage (feature 65) | Medium | 90 days |
| Semantic graph entities | Reflect stage | Low | Indefinite |
| Semantic graph relationships | Reflect stage | Low | Indefinite |
| Sentinel events (processed) | Monitors (feature 80) | High | 7 days |
| Sentinel raw data (clipboard, etc.) | Monitors | Critical | Never stored in raw form |
| Forged tool source code | Tool Forge (feature 100) | Low | Indefinite (user-approved) |
| Pipeline traces | Telemetry (feature 85) | Medium | 30 days |
| Decision records | Telemetry | Medium | 30 days |
| Aggregated metrics | Telemetry | Low | Indefinite |
| Window titles | Active Window Monitor | High | Never stored (except allowed apps) |
| File paths | File System Monitor | Medium | 7 days |
| Calendar entries | Calendar Monitor | High | 24 hours after event |

### The Right to Be Forgotten

Even in a single-user system, the user should be able to say "forget everything about Project X" and have the system comprehensively remove:
- All conversation messages mentioning it
- All memories referencing it
- All Synapse Graph entities and relationships linked to it
- All Sentinel events triggered by it
- All pipeline traces from interactions about it

This is not just GDPR compliance — it's a feature that builds trust.

## Solution

### Architecture Overview

```
┌─────────────────────────────────────────────────┐
│               Privacy Controller                 │
│                                                  │
│  Consent Manager ─── Data Inventory ─── Purger  │
│       │                    │               │     │
│       ▼                    ▼               ▼     │
│  ┌──────────┐     ┌──────────────┐  ┌─────────┐ │
│  │ Settings │     │  Data Audit  │  │ Cascade  │ │
│  │   Store  │     │    Index     │  │ Delete   │ │
│  └──────────┘     └──────────────┘  └─────────┘ │
└────────────────────┬────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   ┌─────────┐ ┌──────────┐ ┌──────────┐
   │Sentinel │ │  Memory  │ │Telemetry │
   │Monitors │ │  Store   │ │Collector │
   └─────────┘ └──────────┘ └──────────┘
```

### Components

#### 1. Privacy Settings (Domain)

User-configurable privacy preferences stored in the database.

```csharp
public sealed class PrivacySettings : Entity
{
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentGivenAt { get; set; }

    public bool ClipboardMonitorEnabled { get; set; }
    public bool FileSystemMonitorEnabled { get; set; }
    public bool CalendarMonitorEnabled { get; set; }
    public bool ActiveWindowMonitorEnabled { get; set; }

    public int EpisodicMemoryRetentionDays { get; set; } = 90;
    public int SentinelEventRetentionDays { get; set; } = 7;
    public int PipelineTraceRetentionDays { get; set; } = 30;
    public int CalendarEventRetentionHours { get; set; } = 24;

    public List<string> ExcludedApplications { get; set; } = [];
    public List<string> ExcludedFilePaths { get; set; } = [];
    public bool PauseAllMonitoring { get; set; }
}
```

#### 2. Consent Manager (Application)

Handles first-run consent and ongoing consent changes.

```csharp
public interface IConsentManager
{
    Task<bool> HasConsentAsync(CancellationToken ct);

    Task RequestConsentAsync(CancellationToken ct);

    Task UpdateMonitorConsentAsync(
        string monitorName,
        bool enabled,
        CancellationToken ct);

    Task<PrivacySettings> GetSettingsAsync(CancellationToken ct);

    Task UpdateSettingsAsync(
        PrivacySettings settings,
        CancellationToken ct);
}
```

**First-run flow:**
1. On first startup, `leontes init` (feature 30) presents the privacy consent step
2. Each monitor type is listed with a clear explanation of what it collects
3. User enables/disables each monitor individually
4. Consent timestamp is recorded
5. No monitoring starts until consent is explicitly given

**Ongoing consent:** User can change settings at any time via `leontes privacy` CLI command or API endpoint. Changes take effect immediately — if a monitor is disabled, it stops within the current polling cycle.

#### 3. Data Inventory (Application)

Provides a structured view of all data the agent has collected, grouped by category.

```csharp
public interface IDataInventory
{
    Task<DataInventoryReport> GenerateReportAsync(CancellationToken ct);

    Task<DataSearchResult> SearchAsync(
        string query,
        CancellationToken ct);
}

public sealed record DataInventoryReport(
    int ConversationCount,
    int MessageCount,
    int EpisodicMemoryCount,
    int SemanticEntityCount,
    int SemanticRelationshipCount,
    int SentinelEventCount,
    int ForgedToolCount,
    int PipelineTraceCount,
    StorageUsage DiskUsage,
    DateTime OldestRecord,
    DateTime NewestRecord);

public sealed record StorageUsage(
    long ConversationsBytes,
    long MemoriesBytes,
    long GraphBytes,
    long TelemetryBytes,
    long TotalBytes);
```

**CLI integration:** `leontes privacy report` displays the inventory in a human-readable table.

#### 4. Data Purger (Infrastructure)

Handles both automatic retention enforcement and user-initiated deletion.

```csharp
public interface IDataPurger
{
    Task PurgeExpiredAsync(CancellationToken ct);

    Task PurgeByTopicAsync(
        string topic, CancellationToken ct);

    Task PurgeByDateRangeAsync(
        DateTime from, DateTime to, CancellationToken ct);

    Task PurgeAllAsync(CancellationToken ct);
}
```

**Automatic retention:** Runs as a background service every 6 hours. Deletes:
- Episodic memories older than `EpisodicMemoryRetentionDays`
- Sentinel events older than `SentinelEventRetentionDays`
- Pipeline traces older than `PipelineTraceRetentionDays`
- Calendar-sourced data older than `CalendarEventRetentionHours`

**Topic-based purge ("forget Project X"):** Uses full-text search across:
- `Messages.Content` — delete matching messages
- `MemoryEntries.Content` — delete matching memories
- `SynapseEntities.Name` — delete matching entities and all their relationships
- `SentinelEvents.Metadata` — delete matching events
- `PipelineTraces` — delete traces from conversations that contained the topic

**Cascade rules:** When a `SynapseEntity` is deleted, all `SynapseRelationships` referencing it are also deleted. When a `Conversation` is deleted, all its `Messages`, related `MemoryEntries`, and `PipelineTraces` are also deleted.

#### 5. Data Export (Infrastructure)

Exports all user data in a portable, human-readable format.

```csharp
public interface IDataExporter
{
    Task<string> ExportAllAsync(
        string outputDirectory, CancellationToken ct);

    Task<string> ExportConversationsAsync(
        string outputDirectory, CancellationToken ct);

    Task<string> ExportMemoriesAsync(
        string outputDirectory, CancellationToken ct);

    Task<string> ExportGraphAsync(
        string outputDirectory, CancellationToken ct);

    Task<string> ExportToolsAsync(
        string outputDirectory, CancellationToken ct);
}
```

**Export format:**

```
leontes-export-2026-04-14/
├── manifest.json              (export metadata, version, date)
├── conversations/
│   ├── index.json             (conversation list with metadata)
│   └── {conversationId}.md    (messages in Markdown format)
├── memories/
│   ├── episodic.json          (episodic memory entries)
│   └── summaries.json         (consolidated summaries)
├── graph/
│   ├── entities.json          (all Synapse entities)
│   └── relationships.json     (all relationships)
├── tools/
│   ├── catalog.json           (tool metadata)
│   └── source/
│       └── {toolName}.cs      (source code of each forged tool)
└── metrics/
    └── summaries.json         (aggregated metrics history)
```

**CLI integration:** `leontes export [--path <dir>]` creates the export archive. Default path: `~/leontes-export-{date}/`.

#### 6. Privacy Pause Mode (Application)

Instant pause of all monitoring — a "do not disturb" for data collection.

```csharp
public interface IPrivacyPauseService
{
    Task PauseAsync(TimeSpan? duration, CancellationToken ct);
    Task ResumeAsync(CancellationToken ct);
    Task<bool> IsPausedAsync(CancellationToken ct);
}
```

**Behavior when paused:**
- All Sentinel monitors stop polling
- No new memories are created by the Reflect stage
- Conversations still work (chat is always available)
- No Synapse Graph updates
- Auto-resumes after duration expires (if specified)
- Keyboard shortcut or CLI command: `leontes pause [--minutes 30]`

#### 7. Sensitive Data Filter (Infrastructure)

Applied at the boundary of every data persistence operation. Ensures sensitive content is never stored in plaintext.

```csharp
public interface ISensitiveDataFilter
{
    string Redact(string content);
    string HashFingerprint(string content);
    bool IsSensitive(string content);
}
```

**Patterns detected and redacted:**
- Credit card numbers → `[REDACTED:CC]`
- IBANs → `[REDACTED:IBAN]`
- Email addresses → preserved (needed for entity resolution)
- Phone numbers → preserved (needed for entity resolution)
- Passwords (from clipboard in password manager context) → never stored
- API keys / tokens (patterns like `sk-...`, `ghp_...`) → `[REDACTED:TOKEN]`
- SSNs / national IDs → `[REDACTED:ID]`

**Application points:**
- Before writing `MemoryEntry.Content`
- Before writing `SentinelEvent.Metadata`
- Before writing `DecisionRecord.Rationale` (in case it contains user data)
- Before logging any structured data at Debug level

### API Endpoints

```
GET    /api/v1/privacy/settings               → PrivacySettings
PUT    /api/v1/privacy/settings               → PrivacySettings (update)
POST   /api/v1/privacy/consent                → ConsentResult
GET    /api/v1/privacy/report                 → DataInventoryReport
POST   /api/v1/privacy/export                 → ExportResult (path to archive)
DELETE /api/v1/privacy/data                   → PurgeResult (delete everything)
DELETE /api/v1/privacy/data/topic/{topic}     → PurgeResult (topic-based purge)
DELETE /api/v1/privacy/data/range             → PurgeResult (date range purge)
POST   /api/v1/privacy/pause                  → PauseResult
POST   /api/v1/privacy/resume                 → ResumeResult
GET    /api/v1/privacy/status                 → PrivacyStatus (paused? monitors active?)
```

### CLI Commands

```bash
leontes privacy                    # Show current privacy status and settings
leontes privacy report             # Data inventory summary
leontes privacy settings           # Interactive settings editor
leontes privacy forget "Project X" # Topic-based purge with confirmation
leontes privacy export             # Export all data
leontes privacy delete-all         # Nuclear option with double confirmation
leontes pause [--minutes N]        # Pause all monitoring
leontes resume                     # Resume monitoring
```

### Data Model

#### PrivacySettings Table

```sql
CREATE TABLE "PrivacySettings" (
    "Id"                            uuid PRIMARY KEY,
    "ConsentGiven"                  boolean NOT NULL DEFAULT false,
    "ConsentGivenAt"                timestamptz,
    "ClipboardMonitorEnabled"       boolean NOT NULL DEFAULT false,
    "FileSystemMonitorEnabled"      boolean NOT NULL DEFAULT false,
    "CalendarMonitorEnabled"        boolean NOT NULL DEFAULT false,
    "ActiveWindowMonitorEnabled"    boolean NOT NULL DEFAULT false,
    "EpisodicMemoryRetentionDays"   int NOT NULL DEFAULT 90,
    "SentinelEventRetentionDays"    int NOT NULL DEFAULT 7,
    "PipelineTraceRetentionDays"    int NOT NULL DEFAULT 30,
    "CalendarEventRetentionHours"   int NOT NULL DEFAULT 24,
    "ExcludedApplications"          jsonb NOT NULL DEFAULT '[]',
    "ExcludedFilePaths"             jsonb NOT NULL DEFAULT '[]',
    "PauseAllMonitoring"            boolean NOT NULL DEFAULT false,
    "PauseExpiresAt"                timestamptz,
    "Created"                       timestamptz NOT NULL,
    "CreatedBy"                     uuid NOT NULL,
    "LastModified"                  timestamptz,
    "LastModifiedBy"                uuid
);
```

#### DataPurgeLog Table

```sql
CREATE TABLE "DataPurgeLog" (
    "Id"            uuid PRIMARY KEY,
    "PurgeType"     text NOT NULL,
    "Scope"         text NOT NULL,
    "RecordsDeleted" int NOT NULL,
    "TriggeredBy"   text NOT NULL,
    "Created"       timestamptz NOT NULL,
    "CreatedBy"     uuid NOT NULL
);

CREATE INDEX "IX_DataPurgeLog_Created" ON "DataPurgeLog" ("Created" DESC);
```

`PurgeType`: `Automatic`, `TopicBased`, `DateRange`, `Full`
`TriggeredBy`: `RetentionService`, `UserRequest`

### Migration

```bash
dotnet ef migrations add AddPrivacyGovernanceTables \
    --project backend/src/Leontes.Infrastructure \
    --startup-project backend/src/Leontes.Api
```

### Configuration

```json
{
  "Privacy": {
    "RetentionCheckIntervalHours": 6,
    "ExportDefaultPath": "~/leontes-export",
    "SensitivePatterns": {
      "CreditCard": "\\b\\d{4}[- ]?\\d{4}[- ]?\\d{4}[- ]?\\d{4}\\b",
      "IBAN": "\\b[A-Z]{2}\\d{2}[A-Z0-9]{4,30}\\b",
      "ApiToken": "\\b(sk-|ghp_|gho_|glpat-)[A-Za-z0-9_-]{20,}\\b",
      "SSN": "\\b\\d{3}-\\d{2}-\\d{4}\\b"
    }
  }
}
```

## Acceptance Criteria

- [ ] First run blocks until explicit privacy consent is given
- [ ] Each Sentinel monitor is independently opt-in
- [ ] User can view a data inventory showing all stored data categories and counts
- [ ] User can delete data by topic ("forget Project X") with cascade across all tables
- [ ] User can delete data by date range
- [ ] User can export all data in human-readable format (JSON + Markdown)
- [ ] Automatic retention enforcement runs periodically and hard-deletes expired data
- [ ] Sensitive data (credit cards, tokens, IBANs) is never stored in plaintext
- [ ] Privacy pause mode stops all monitoring immediately
- [ ] Privacy pause auto-resumes after specified duration
- [ ] Excluded applications list prevents window title logging
- [ ] Data purge log records every deletion for audit
- [ ] CLI `leontes privacy` commands work for all operations
- [ ] API endpoints expose all privacy operations
- [ ] Changing privacy settings takes effect within the current monitor polling cycle

## Out of Scope

- Encryption at rest (rely on PostgreSQL and OS-level disk encryption)
- Multi-user privacy boundaries (single-user system)
- GDPR-specific compliance documentation (this is a personal tool, not a SaaS)
- Remote wipe capability
- Privacy impact assessment automation
