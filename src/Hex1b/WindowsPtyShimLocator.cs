using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Hex1b;

internal static class WindowsPtyShimLocator
{
    private const string ShimExecutableName = "hex1bpty.exe";

    public static bool TryResolve([NotNullWhen(true)] out string? path)
    {
        return TryResolve(explicitPath: null, out path);
    }

    public static bool TryResolve(string? explicitPath, [NotNullWhen(true)] out string? path)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
            {
                path = explicitPath;
                return true;
            }

            path = null;
            return false;
        }

        var appBaseDirectory = AppContext.BaseDirectory;
        var primaryRid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        var fallbackRid = primaryRid == "win-arm64" ? "win-x64" : null;
        var candidates = BuildCandidatePaths(appBaseDirectory, primaryRid, fallbackRid);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = null;
        return false;
    }

    private static IEnumerable<string> BuildCandidatePaths(
        string appBaseDirectory,
        string primaryRid,
        string? fallbackRid)
    {
        yield return Path.Combine(appBaseDirectory, ShimExecutableName);
        yield return Path.Combine(appBaseDirectory, "runtimes", primaryRid, "native", ShimExecutableName);

        if (!string.IsNullOrWhiteSpace(fallbackRid))
        {
            yield return Path.Combine(appBaseDirectory, "runtimes", fallbackRid!, "native", ShimExecutableName);
        }

        var current = appBaseDirectory;
        for (var depth = 0; depth < 6 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            yield return Path.Combine(current, "src", "Hex1b", "obj", "windows-pty-shim", primaryRid, "native", ShimExecutableName);
            yield return Path.Combine(current, "artifacts", "windows-pty-shim", primaryRid, ShimExecutableName);

            if (!string.IsNullOrWhiteSpace(fallbackRid))
            {
                yield return Path.Combine(current, "src", "Hex1b", "obj", "windows-pty-shim", fallbackRid!, "native", ShimExecutableName);
                yield return Path.Combine(current, "artifacts", "windows-pty-shim", fallbackRid!, ShimExecutableName);
            }

            current = Path.GetDirectoryName(current);
        }
    }
}
