using System.Text.RegularExpressions;

namespace Hex1b;

/// <summary>
/// Converts Windows filesystem paths to WSL2-compatible paths.
/// </summary>
internal static partial class WslPathHelper
{
    // Matches a Windows absolute path like C:\foo or D:\bar at the start of a string
    [GeneratedRegex(@"^([A-Za-z]):\\")]
    private static partial Regex WindowsDrivePrefix();

    /// <summary>
    /// Converts a Windows path to a WSL2 mount path.
    /// </summary>
    /// <param name="windowsPath">A Windows filesystem path (e.g. <c>C:\Users\me\code</c>).</param>
    /// <returns>
    /// The equivalent WSL2 path (e.g. <c>/mnt/c/Users/me/code</c>), or the
    /// original path unchanged if it is not a Windows absolute path.
    /// </returns>
    public static string ConvertToWslPath(string windowsPath)
    {
        var match = WindowsDrivePrefix().Match(windowsPath);
        if (!match.Success)
        {
            return windowsPath;
        }

        var driveLetter = match.Groups[1].Value.ToLowerInvariant();
        var remainder = windowsPath[match.Length..].Replace('\\', '/');
        return $"/mnt/{driveLetter}/{remainder}";
    }

    /// <summary>
    /// Converts a Docker volume mount spec, translating the host path portion
    /// from Windows to WSL2 format.
    /// </summary>
    /// <param name="volumeSpec">
    /// A volume spec in the form <c>host_path:container_path[:options]</c>.
    /// </param>
    /// <returns>The volume spec with the host path converted to a WSL2 path.</returns>
    public static string ConvertVolumeSpec(string volumeSpec)
    {
        // Volume specs look like: C:\Users\me\code:/workspace:rw
        // We need to convert only the host path (before first colon that's not
        // part of a drive letter like C:).
        //
        // Strategy: if the spec starts with a drive letter (X:\...), split after
        // the first colon that follows the drive letter prefix.
        var driveMatch = WindowsDrivePrefix().Match(volumeSpec);
        if (!driveMatch.Success)
        {
            return volumeSpec;
        }

        // Find the colon that separates host:container (skip the drive letter colon)
        var separatorIndex = volumeSpec.IndexOf(':', driveMatch.Length);
        if (separatorIndex < 0)
        {
            // No container path separator — just convert the whole thing
            return ConvertToWslPath(volumeSpec);
        }

        var hostPath = volumeSpec[..separatorIndex];
        var rest = volumeSpec[separatorIndex..]; // includes the leading ':'
        return ConvertToWslPath(hostPath) + rest;
    }

    /// <summary>
    /// If an argument looks like a Windows absolute path, convert it for WSL.
    /// Used for docker build args like the Dockerfile path and build context.
    /// </summary>
    /// <param name="arg">A command-line argument.</param>
    /// <returns>The argument with Windows paths converted to WSL paths.</returns>
    public static string ConvertArgIfPath(string arg)
    {
        if (WindowsDrivePrefix().IsMatch(arg))
        {
            return ConvertToWslPath(arg);
        }

        return arg;
    }
}
