# 105 — Structural Vision

## Problem

The assistant has no way to perceive what the user is looking at on screen. Without vision, it cannot answer questions like "what's the error in this dialog?" or "fill in this form" or "what app am I in?" Screenshots and OCR are brittle, coordinate-dependent, and break across resolutions. The assistant needs structured perception — reading the UI as a semantic tree of elements, not a grid of pixels.

## Prerequisites

- Working Sentinel infrastructure (feature 90) — Active Window Monitor provides window focus context
- Thinking Pipeline (feature 70) — Vision data feeds into the Perceive stage

## Rules

- No screenshots, no OCR, no pixel coordinates — use Windows UI Automation (UIA) exclusively
- Use `System.Windows.Automation` namespace (built into .NET on Windows) — no new NuGet packages
- Vision is read-only — the assistant can observe UI state but cannot interact with it (clicking, typing) in this feature
- UI tree serialization must be concise enough to fit in an LLM context window (target: <4000 tokens for a typical window)
- Vision is opt-in — the user must explicitly ask for screen context or enable it in Sentinel
- Never capture or log content from password fields, banking applications, or windows matching a privacy exclusion list

## Background

### Why UI Automation Over Screenshots

| Approach | Pros | Cons |
|---|---|---|
| Screenshots + OCR | Works on any app, captures visual layout | Resolution-dependent, expensive (vision model), no semantic structure, privacy risk |
| Screenshots + Vision LLM | Can "understand" images | Very expensive per call, high latency, still no semantic structure |
| UI Automation tree | Semantic structure, element IDs, control types, states, free | Windows-only, some apps have poor accessibility markup |
| Accessibility APIs | Same as UIA, cross-platform potential | Platform-specific implementations needed |

UI Automation gives us a **structured, semantic representation** of what's on screen — buttons have labels, text fields have values, menus have items. This is exactly what an LLM needs to reason about UI, without the cost or fragility of vision models.

### OpenClaw's "Semantic Snapshots"

OpenClaw uses a Tree Walker to parse the Accessibility/UI Automation tree into a condensed Markdown representation. Example:

```
[Window: Visual Studio Code - main.cs]
  [MenuBar]
    [MenuItem: File] [MenuItem: Edit] [MenuItem: View]
  [TabGroup]
    [Tab: main.cs (active)] [Tab: Program.cs]
  [Editor: main.cs]
    [Text: "public class Main {"]
    [Text: "    // TODO: implement"]
    [Text: "}"]
  [StatusBar]
    [Text: "Ln 2, Col 5"] [Text: "C#"] [Text: "UTF-8"]
```

This is compact, semantic, and gives the LLM everything it needs to reason about the UI without seeing a single pixel.

### "A Thousand Brains" (Jeff Hawkins) — Reference Frames

The brain doesn't store a single "map" of the visual world — it maintains thousands of individual models ("reference frames") that vote on what's being observed. For Structural Vision:
- Each UI element is a reference frame with properties (type, name, value, state, bounds)
- The tree structure provides spatial relationships (parent-child, sibling order)
- The LLM votes on interpretation using these structured frames, not raw pixels

## Solution

### Architecture Overview

```
User asks: "What error is showing?" or Sentinel detects app switch
    |
    v
[ActiveWindowMonitor] — identifies foreground window (PID, process name)
    |
    v
[UITreeWalker] — traverses UI Automation tree from root element
    |
    v
[TreeSerializer] — converts tree to condensed text representation
    |  applies depth limit, element filtering, privacy exclusions
    v
[ThinkingContext.ScreenState] — structured UI snapshot added to context
    |
    v
[Perceive Stage] — injects screen state into LLM prompt
    |
    v
[LLM] — reasons about UI structure semantically
```

### Components

#### 1. UIElement (Domain Layer)

Simplified representation of a UI Automation element:

```csharp
public sealed record UIElement(
    string ControlType,
    string? Name,
    string? Value,
    string? AutomationId,
    bool IsEnabled,
    bool IsOffscreen,
    Rect? BoundingRectangle,
    IReadOnlyList<UIElement> Children);

public readonly record struct Rect(double X, double Y, double Width, double Height);
```

#### 2. IUITreeWalker (Application Layer)

```csharp
public interface IUITreeWalker
{
    Task<UIElement?> CaptureWindowTreeAsync(
        int processId,
        TreeWalkerOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<UIElement?> CaptureFocusedWindowTreeAsync(
        TreeWalkerOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record TreeWalkerOptions(
    int MaxDepth = 8,
    bool IncludeOffscreen = false,
    bool IncludeValues = true,
    IReadOnlyList<string>? ExcludeControlTypes = null);
```

#### 3. ITreeSerializer (Application Layer)

```csharp
public interface ITreeSerializer
{
    string Serialize(UIElement root, TreeSerializerOptions? options = null);
}

public sealed record TreeSerializerOptions(
    TreeOutputFormat Format = TreeOutputFormat.CompactMarkdown,
    int MaxTokenEstimate = 4000,
    bool IncludeBounds = false);

public enum TreeOutputFormat
{
    CompactMarkdown,
    Json,
    IndentedText
}
```

#### 4. UIAutomationTreeWalker (Infrastructure Layer)

```csharp
public sealed class UIAutomationTreeWalker(
    ILogger<UIAutomationTreeWalker> logger) : IUITreeWalker
{
    public Task<UIElement?> CaptureFocusedWindowTreeAsync(
        TreeWalkerOptions? options,
        CancellationToken cancellationToken)
    {
        var root = AutomationElement.FocusedElement;
        if (root is null) return Task.FromResult<UIElement?>(null);

        // Walk up to the window level
        var window = TreeWalker.ControlViewWalker
            .GetParent(root); // walk up to Window element

        var tree = WalkTree(window, options ?? new(), depth: 0);
        return Task.FromResult(tree);
    }

    private UIElement? WalkTree(
        AutomationElement element,
        TreeWalkerOptions options,
        int depth)
    {
        if (depth > options.MaxDepth) return null;
        if (!options.IncludeOffscreen && IsOffscreen(element)) return null;

        var children = new List<UIElement>();
        var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
        while (child is not null)
        {
            var childElement = WalkTree(child, options, depth + 1);
            if (childElement is not null)
                children.Add(childElement);
            child = TreeWalker.ControlViewWalker.GetNextSibling(child);
        }

        return new UIElement(
            ControlType: element.Current.ControlType.ProgrammaticName,
            Name: NullIfEmpty(element.Current.Name),
            Value: options.IncludeValues ? GetValue(element) : null,
            AutomationId: NullIfEmpty(element.Current.AutomationId),
            IsEnabled: element.Current.IsEnabled,
            IsOffscreen: element.Current.IsOffscreen,
            BoundingRectangle: ToBounds(element.Current.BoundingRectangle),
            Children: children);
    }
}
```

#### 5. CompactMarkdownSerializer (Infrastructure Layer)

Converts the tree to a token-efficient text format:

```csharp
public sealed class CompactMarkdownSerializer : ITreeSerializer
{
    public string Serialize(UIElement root, TreeSerializerOptions? options)
    {
        var sb = new StringBuilder();
        SerializeElement(sb, root, indent: 0, options ?? new());
        return sb.ToString();
    }

    private void SerializeElement(
        StringBuilder sb,
        UIElement element,
        int indent,
        TreeSerializerOptions options)
    {
        var prefix = new string(' ', indent * 2);
        var label = FormatElement(element);

        if (!string.IsNullOrEmpty(label))
            sb.AppendLine($"{prefix}{label}");

        foreach (var child in element.Children)
            SerializeElement(sb, child, indent + 1, options);
    }

    private static string FormatElement(UIElement element)
    {
        // [ControlType: Name] or [ControlType: Name = "Value"]
        var type = element.ControlType.Replace("ControlType.", "");
        var name = element.Name ?? element.AutomationId ?? "";
        var value = element.Value is not null ? $" = \"{Truncate(element.Value, 100)}\"" : "";

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(element.Value))
            return $"[{type}]";

        return $"[{type}: {name}{value}]";
    }
}
```

Example output:
```
[Window: Visual Studio Code - Program.cs]
  [MenuBar]
    [MenuItem: File] [MenuItem: Edit] [MenuItem: View]
  [Tab: Program.cs (selected)]
  [Edit: Editor]
    [Text = "using System;"]
    [Text = "namespace Leontes.Api;"]
  [StatusBar]
    [Text = "Ln 5, Col 1"] [Text = "C#"]
  [Pane: Problems]
    [TreeItem: error CS1002: ; expected - Program.cs(12,1)]
```

### Pipeline Integration

Vision data is added to `ThinkingContext`:

```csharp
// In ThinkingContext (feature 70)
public string? ScreenState { get; set; }
```

**Perceive Stage** captures screen state when:
1. User explicitly asks about the screen ("what error is showing?", "what am I looking at?")
2. Sentinel's ActiveWindowMonitor detects a relevant app switch
3. A tool requests screen context

```csharp
// In PerceiveStage
if (RequiresScreenContext(context.UserMessage.Content))
{
    var tree = await uiTreeWalker.CaptureFocusedWindowTreeAsync(cancellationToken: cancellationToken);
    if (tree is not null)
    {
        context.ScreenState = treeSerializer.Serialize(tree);
    }
}
```

The serialized tree is injected into the LLM system prompt:

```
## Current Screen State
The user is looking at:
{serialized tree}

Use this context to answer questions about what's visible on screen.
```

### Privacy & Security

**Privacy Exclusion List:** Windows from certain processes are never captured:

```json
{
    "Vision": {
        "ExcludedProcesses": [
            "1password", "keepass", "bitwarden", "lastpass",
            "mstsc", "vmconnect",
            "msedge", "chrome", "firefox"
        ],
        "ExcludePasswordFields": true,
        "RequireExplicitRequest": true
    }
}
```

- Browsers are excluded by default (too much sensitive content) — user can whitelist specific URLs later
- Password fields (`ControlType.Password`) are always excluded — value is replaced with `[password field]`
- Remote desktop windows are excluded (could expose other users' screens)
- All vision captures are ephemeral — stored only in `ThinkingContext` for the current request, never persisted

### Configuration

```json
{
    "Vision": {
        "Enabled": false,
        "MaxTreeDepth": 8,
        "MaxTokenEstimate": 4000,
        "IncludeBounds": false,
        "RequireExplicitRequest": true,
        "ExcludedProcesses": ["1password", "keepass", "bitwarden", "msedge", "chrome", "firefox"]
    }
}
```

Vision is disabled by default and requires explicit opt-in. When `RequireExplicitRequest` is true, the Perceive stage only captures screen state when the user's message explicitly asks about the screen.

### Error Handling

| Scenario | Behavior |
|---|---|
| UI Automation access denied (permissions) | Log warning, return null tree, LLM responds without screen context |
| Target window has no UIA support | Return minimal tree (window title + process name only) |
| Tree exceeds token budget | Truncate deepest nodes first, keep top-level structure |
| Excluded process detected | Skip capture, log at Debug level |
| Element properties throw COMException | Skip element, continue walking siblings |

## Acceptance Criteria

- [ ] `IUITreeWalker` captures the focused window's UI Automation tree
- [ ] `ITreeSerializer` produces compact Markdown representation fitting within 4000 tokens
- [ ] Password fields and excluded processes are never captured
- [ ] Perceive stage injects screen state when user asks about the screen
- [ ] LLM can answer questions about visible UI elements (error dialogs, form fields, menu items)
- [ ] Tree walking handles apps with poor accessibility markup gracefully (returns partial tree)
- [ ] Vision is disabled by default and requires explicit opt-in
- [ ] No vision data is persisted to database or logs
- [ ] `leontes init` includes a step to enable/disable Structural Vision and configure excluded processes

## Out of Scope

- UI interaction (clicking buttons, filling forms, keyboard input) — read-only in this feature
- Screenshot capture or OCR fallback
- Cross-platform support (macOS, Linux) — Windows UI Automation only
- Browser DOM inspection (browsers are excluded by default)
- Multi-monitor support (captures focused window only)
- Video/animation capture
- Vision for Signal channel (mobile has no UI Automation access)
