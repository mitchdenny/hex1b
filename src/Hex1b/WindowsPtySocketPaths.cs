using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace Hex1b;

internal static class WindowsPtySocketPaths
{
    private const string SocketDirectoryEnvironmentVariable = "HEX1B_PTY_SHIM_SOCKET_DIR";

    public static string GetSocketDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(SocketDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.GetFullPath(overrideDirectory);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(home, ".hex1b", "hex1bpty");
    }

    public static string CreateSocketPath()
    {
        var socketDirectory = GetSocketDirectory();
        EnsureSocketDirectoryExists(socketDirectory);

        var socketPath = Path.Combine(socketDirectory, $"hex1bpty-{Guid.NewGuid():N}.socket");
        DeleteSocketFile(socketPath);
        return socketPath;
    }

    public static void EnsureSocketDirectoryExistsForPath(string socketPath)
    {
        var socketDirectory = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrWhiteSpace(socketDirectory))
        {
            EnsureSocketDirectoryExists(socketDirectory);
        }
    }

    public static void EnsureSocketDirectoryExists(string socketDirectory)
    {
        Directory.CreateDirectory(socketDirectory);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        TryRestrictDirectoryToCurrentUser(socketDirectory);
    }

    public static void DeleteSocketFile(string? socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            return;
        }

        try
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TryRestrictDirectoryToCurrentUser(string socketDirectory)
    {
        try
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is null)
            {
                return;
            }

            var directorySecurity = new DirectorySecurity();
            directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            const FileSystemRights rights = FileSystemRights.FullControl;
            const InheritanceFlags inheritance =
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                rights,
                inheritance,
                PropagationFlags.None,
                AccessControlType.Allow));

            var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                localSystemSid,
                rights,
                inheritance,
                PropagationFlags.None,
                AccessControlType.Allow));

            new DirectoryInfo(socketDirectory).SetAccessControl(directorySecurity);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
