namespace Hex1b.Kgp;

internal static class KgpDebugLog
{
    private static readonly object s_lock = new();

    internal static bool IsEnabled => GetLogPath() is not null;

    internal static void Write(string message)
    {
        var path = GetLogPath();
        if (path is null)
            return;

        lock (s_lock)
        {
            File.AppendAllText(path, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
    }

    private static string? GetLogPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("HEX1B_KGP_DEBUG_LOG");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        return Environment.GetEnvironmentVariable("HEX1B_KGP_DEBUG") == "1"
            ? "/tmp/hex1b-kgp-debug.log"
            : null;
    }
}
