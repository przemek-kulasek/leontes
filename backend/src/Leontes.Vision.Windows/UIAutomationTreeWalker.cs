using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Leontes.Application.Vision;
using Leontes.Domain.Vision;
using Microsoft.Extensions.Logging;
using DomainRect = Leontes.Domain.Vision.Rect;

namespace Leontes.Vision.Windows;

/// <summary>
/// Windows UI Automation implementation of <see cref="IUITreeWalker"/>.
/// Walks the native accessibility tree of the focused window using the ControlView walker,
/// redacts password fields, and honors the configured process exclusion list.
/// </summary>
public sealed class UIAutomationTreeWalker(
    VisionOptions options,
    ILogger<UIAutomationTreeWalker> logger) : IUITreeWalker
{
    private const string PasswordPlaceholder = "[password field]";

    private readonly VisionOptions _options = options;

    public Task<UIElement?> CaptureFocusedWindowTreeAsync(
        TreeWalkerOptions? walkerOptions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                logger.LogDebug("Vision: no focused UI Automation element available.");
                return Task.FromResult<UIElement?>(null);
            }

            var window = WalkUpToWindow(focused) ?? focused;
            return Task.FromResult(CaptureFromRoot(window, walkerOptions, cancellationToken));
        }
        catch (ElementNotAvailableException ex)
        {
            logger.LogDebug(ex, "Vision: focused element became unavailable during capture.");
            return Task.FromResult<UIElement?>(null);
        }
        catch (COMException ex)
        {
            logger.LogWarning(ex, "Vision: COM error while reading focused window.");
            return Task.FromResult<UIElement?>(null);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Vision: access denied reading UI Automation tree.");
            return Task.FromResult<UIElement?>(null);
        }
    }

    public Task<UIElement?> CaptureWindowTreeAsync(
        int processId,
        TreeWalkerOptions? walkerOptions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            var root = AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
            return Task.FromResult(CaptureFromRoot(root, walkerOptions, cancellationToken));
        }
        catch (ElementNotAvailableException ex)
        {
            logger.LogDebug(ex, "Vision: element for process {ProcessId} unavailable.", processId);
            return Task.FromResult<UIElement?>(null);
        }
        catch (COMException ex)
        {
            logger.LogWarning(ex, "Vision: COM error while reading window for process {ProcessId}.", processId);
            return Task.FromResult<UIElement?>(null);
        }
    }

    private UIElement? CaptureFromRoot(
        AutomationElement? root,
        TreeWalkerOptions? walkerOptions,
        CancellationToken cancellationToken)
    {
        if (root is null)
            return null;

        if (IsProcessExcluded(root))
        {
            logger.LogDebug("Vision: skipping capture — process is on exclusion list.");
            return null;
        }

        var opts = walkerOptions ?? new TreeWalkerOptions(MaxDepth: _options.MaxTreeDepth);
        return WalkTree(root, opts, depth: 0, cancellationToken);
    }

    private UIElement? WalkTree(
        AutomationElement element,
        TreeWalkerOptions options,
        int depth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > options.MaxDepth)
            return null;

        bool isOffscreen;
        string controlTypeName;
        string? name;
        string? automationId;
        bool isEnabled;
        DomainRect? bounds;

        try
        {
            isOffscreen = SafeGet(() => element.Current.IsOffscreen);
            if (!options.IncludeOffscreen && isOffscreen)
                return null;

            controlTypeName = SafeGet(() => element.Current.ControlType?.ProgrammaticName) ?? "ControlType.Custom";

            if (options.ExcludeControlTypes is { Count: > 0 } &&
                options.ExcludeControlTypes.Contains(SimplifyControlType(controlTypeName), StringComparer.OrdinalIgnoreCase))
                return null;

            name = NullIfEmpty(SafeGet(() => element.Current.Name));
            automationId = NullIfEmpty(SafeGet(() => element.Current.AutomationId));
            isEnabled = SafeGet(() => element.Current.IsEnabled);
            bounds = ToRect(SafeGet(() => element.Current.BoundingRectangle));
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }

        var value = options.IncludeValues ? ReadValue(element, controlTypeName) : null;

        var children = new List<UIElement>();
        try
        {
            var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            while (child is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var mapped = WalkTree(child, options, depth + 1, cancellationToken);
                if (mapped is not null)
                    children.Add(mapped);

                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
        catch (ElementNotAvailableException)
        {
            // Tree mutated while walking — return what we have.
        }

        return new UIElement(
            ControlType: controlTypeName,
            Name: name,
            Value: value,
            AutomationId: automationId,
            IsEnabled: isEnabled,
            IsOffscreen: isOffscreen,
            BoundingRectangle: bounds,
            Children: children);
    }

    private string? ReadValue(AutomationElement element, string controlTypeName)
    {
        if (_options.ExcludePasswordFields && IsPasswordField(element, controlTypeName))
            return PasswordPlaceholder;

        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) &&
                pattern is ValuePattern vp)
            {
                return NullIfEmpty(vp.Current.Value);
            }

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern) &&
                textPattern is TextPattern tp)
            {
                var text = tp.DocumentRange.GetText(maxLength: 200);
                return NullIfEmpty(text);
            }
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (COMException) { }

        return null;
    }

    private static bool IsPasswordField(AutomationElement element, string controlTypeName)
    {
        if (string.Equals(SimplifyControlType(controlTypeName), "Password", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            return (bool)element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, ignoreDefaultValue: true);
        }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (InvalidCastException) { return false; }
    }

    private bool IsProcessExcluded(AutomationElement element)
    {
        if (_options.ExcludedProcesses.Count == 0)
            return false;

        try
        {
            var pid = (int)element.Current.ProcessId;
            using var process = Process.GetProcessById(pid);
            var name = process.ProcessName;

            foreach (var excluded in _options.ExcludedProcesses)
            {
                if (name.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (ElementNotAvailableException) { return false; }

        return false;
    }

    private static AutomationElement? WalkUpToWindow(AutomationElement element)
    {
        var windowTypeId = ControlType.Window.Id;
        var current = element;
        while (current is not null)
        {
            try
            {
                if (current.Current.ControlType?.Id == windowTypeId)
                    return current;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            current = TreeWalker.ControlViewWalker.GetParent(current);
        }
        return null;
    }

    private static DomainRect? ToRect(System.Windows.Rect rect)
    {
        if (rect.IsEmpty || double.IsInfinity(rect.Width) || double.IsInfinity(rect.Height))
            return null;
        return new DomainRect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static string SimplifyControlType(string controlType) =>
        controlType.StartsWith("ControlType.", StringComparison.Ordinal)
            ? controlType.Substring("ControlType.".Length)
            : controlType;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static T SafeGet<T>(Func<T> accessor)
    {
        try { return accessor(); }
        catch (ElementNotAvailableException) { return default!; }
        catch (InvalidOperationException) { return default!; }
        catch (COMException) { return default!; }
    }
}
