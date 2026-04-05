using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Hex1b;

internal static class WindowsPtyShimLocator
{
    private const string ShimPathOverrideEnvironmentVariable = "HEX1B_PTY_SHIM_PATH";
    private const string DisableShimEnvironmentVariable = "HEX1B_DISABLE_WINDOWS_PTY_SHIM";
    private const string ShimImplementationEnvironmentVariable = "HEX1B_PTY_SHIM_IMPL";

    public static bool IsDisabled()
    {
        var value = Environment.GetEnvironmentVariable(DisableShimEnvironmentVariable);
        return value is "1" or "true" or "TRUE" or "True";
    }

    public static bool TryResolve([NotNullWhen(true)] out string? path)
    {
        var overridePath = Environment.GetEnvironmentVariable(ShimPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (File.Exists(overridePath))
            {
                path = overridePath;
                return true;
            }

            path = null;
            return false;
        }

        var appBaseDirectory = AppContext.BaseDirectory;
        var primaryRid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        var fallbackRid = primaryRid == "win-arm64" ? "win-x64" : null;
        var executableNames = GetExecutableNames();
        var candidates = BuildCandidatePaths(appBaseDirectory, primaryRid, fallbackRid, executableNames);

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

    private static IReadOnlyList<string> GetExecutableNames()
    {
        var preference = Environment.GetEnvironmentVariable(ShimImplementationEnvironmentVariable)?.Trim();
        if (preference is not null &&
            (preference.Equals("managed", StringComparison.OrdinalIgnoreCase) ||
             preference.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
             preference.Equals("aot", StringComparison.OrdinalIgnoreCase)))
        {
            return ["hex1bpty-managed.exe", "hex1bpty.exe"];
        }

        if (preference is not null && preference.Equals("rust", StringComparison.OrdinalIgnoreCase))
        {
            return ["hex1bpty.exe", "hex1bpty-managed.exe"];
        }

        return ["hex1bpty.exe", "hex1bpty-managed.exe"];
    }

    private static IEnumerable<string> BuildCandidatePaths(
        string appBaseDirectory,
        string primaryRid,
        string? fallbackRid,
        IReadOnlyList<string> executableNames)
    {
        foreach (var executableName in executableNames)
        {
            yield return Path.Combine(appBaseDirectory, executableName);
            yield return Path.Combine(appBaseDirectory, "runtimes", primaryRid, "native", executableName);
        }

        if (!string.IsNullOrWhiteSpace(fallbackRid))
        {
            foreach (var executableName in executableNames)
            {
                yield return Path.Combine(appBaseDirectory, "runtimes", fallbackRid!, "native", executableName);
            }
        }

        var current = appBaseDirectory;
        for (var depth = 0; depth < 6 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            foreach (var executableName in executableNames)
            {
                yield return Path.Combine(current, "src", "Hex1b", "obj", "windows-pty-shim", primaryRid, "native", executableName);
                yield return Path.Combine(current, "artifacts", "windows-pty-shim", primaryRid, executableName);
            }

            if (!string.IsNullOrWhiteSpace(fallbackRid))
            {
                foreach (var executableName in executableNames)
                {
                    yield return Path.Combine(current, "src", "Hex1b", "obj", "windows-pty-shim", fallbackRid!, "native", executableName);
                    yield return Path.Combine(current, "artifacts", "windows-pty-shim", fallbackRid!, executableName);
                }
            }

            current = Path.GetDirectoryName(current);
        }
    }
}
