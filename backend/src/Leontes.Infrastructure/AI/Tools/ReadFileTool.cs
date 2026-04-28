using System.ComponentModel;

namespace Leontes.Infrastructure.AI.Tools;

public static class ReadFileTool
{
    private const int MaxBytes = 64 * 1024;

    [Description(
        "Read the contents of a text file from the local machine. " +
        "Use when the user asks what is inside a specific file. " +
        "Returns at most 64 KB; longer files are truncated and the truncation is reported.")]
    public static string ReadFile(
        [Description("Absolute or relative path to the file to read.")] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";

        if (!File.Exists(path))
            return $"Error: file not found: {path}";

        try
        {
            var info = new FileInfo(path);
            if (info.Length == 0)
                return "(empty file)";

            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(info.Length, MaxBytes)];
            var read = stream.Read(buffer, 0, buffer.Length);
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

            return info.Length > MaxBytes
                ? text + $"\n[truncated — file is {info.Length} bytes, only first {MaxBytes} shown]"
                : text;
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: access denied to {path}.";
        }
        catch (IOException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
