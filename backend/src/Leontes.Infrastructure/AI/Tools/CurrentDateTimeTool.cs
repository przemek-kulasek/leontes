using System.ComponentModel;

namespace Leontes.Infrastructure.AI.Tools;

public static class CurrentDateTimeTool
{
    [Description("Get the current date and time in UTC. Use this when the user asks about the current date, time, or day of the week.")]
    public static string GetCurrentDateTime() => DateTime.UtcNow.ToString("u");
}
