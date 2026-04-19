namespace Leontes.Domain.Vision;

public sealed record UIElement(
    string ControlType,
    string? Name,
    string? Value,
    string? AutomationId,
    bool IsEnabled,
    bool IsOffscreen,
    Rect? BoundingRectangle,
    IReadOnlyList<UIElement> Children);
