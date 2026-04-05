using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Hex1b;

internal static class WindowsPtyShimLocator
{
    private const string ShimPathOverrideEnvironmentVariable = "HEX1B_PTY_SHIM_PATH";
    private const string DisableShimEnvironmentVariable = "HEX1B_DISABLE_WINDOWS_PTY_SHIM";

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

        var assemblyDirectory = Path.GetDirectoryName(typeof(Hex1bTerminal).Assembly.Location);
        var appBaseDirectory = AppContext.BaseDirectory;
        var primaryRid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        var fallbackRid = primaryRid == "win-arm64" ? "win-x64" : null;

        string?[] candidates =
        [
            Path.Combine(appBaseDirectory, "hex1bpty.exe"),
            Path.Combine(appBaseDirectory, "runtimes", primaryRid, "native", "hex1bpty.exe"),
            fallbackRid is null ? null : Path.Combine(appBaseDirectory, "runtimes", fallbackRid, "native", "hex1bpty.exe"),
            assemblyDirectory is null ? null : Path.Combine(assemblyDirectory, "hex1bpty.exe"),
            assemblyDirectory is null ? null : Path.Combine(assemblyDirectory, "runtimes", primaryRid, "native", "hex1bpty.exe"),
            assemblyDirectory is null || fallbackRid is null ? null : Path.Combine(assemblyDirectory, "runtimes", fallbackRid, "native", "hex1bpty.exe")
        ];

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
}
