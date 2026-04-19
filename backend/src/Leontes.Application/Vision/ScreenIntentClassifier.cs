namespace Leontes.Application.Vision;

/// <summary>
/// Detects whether the user message explicitly requests information about the current screen.
/// Used by the Perceive stage to decide whether to capture UI Automation state.
/// </summary>
public static class ScreenIntentClassifier
{
    private static readonly string[] Triggers =
    [
        "on my screen", "on screen", "on the screen",
        "what am i looking at", "what am i seeing",
        "what's showing", "what is showing",
        "this dialog", "this window", "this error",
        "this form", "this menu", "this app",
        "current window", "active window", "foreground window",
        "read my screen", "see my screen", "check my screen",
        "my screen", "the screen",
        "in notepad", "my notepad", "in my notepad",
        "what's open", "what is open",
        "what do you see", "what can you see",
        "what's on", "what is on"
    ];

    public static bool RequiresScreenContext(string? userContent)
    {
        if (string.IsNullOrWhiteSpace(userContent))
            return false;

        var lowered = userContent.ToLowerInvariant();
        foreach (var trigger in Triggers)
        {
            if (lowered.Contains(trigger))
                return true;
        }

        return false;
    }
}
