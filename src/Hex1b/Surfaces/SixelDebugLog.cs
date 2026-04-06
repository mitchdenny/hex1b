namespace Hex1b.Surfaces;

internal static class SixelDebugLog
{
    private static readonly object s_lock = new();

    internal static void Write(string message)
    {
        var path = GetLogPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (s_lock)
            {
                File.AppendAllText(path, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? GetLogPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("HEX1B_SIXEL_DEBUG_LOG");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        return Environment.GetEnvironmentVariable("HEX1B_SIXEL_DEBUG") == "1"
            ? Path.Combine(Path.GetTempPath(), "hex1b-sixel-debug.log")
            : null;
    }
}
