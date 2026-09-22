using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Hex1b;

internal static class WindowsPtySocketPaths
{
    private const string SocketDirectoryEnvironmentVariable = "HEX1B_PTY_SHIM_SOCKET_DIR";
    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

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

    public static string CreateSocketPath(string? socketPath = null)
    {
        socketPath ??= Path.Combine(GetSocketDirectory(), $"hex1bpty-{Guid.NewGuid():N}.socket");
        ValidateSocketPath(socketPath);
        EnsureSocketDirectoryExistsForPath(socketPath);
        if (Path.Exists(socketPath))
        {
            throw new IOException($"The PTY socket path already exists: '{socketPath}'.");
        }
        return socketPath;
    }

    internal static void ValidateSocketPath(string socketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        if (!Path.IsPathFullyQualified(socketPath) ||
            socketPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ||
            !string.Equals(Path.GetFullPath(socketPath), socketPath, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(Path.GetFileName(socketPath)))
        {
            throw new ArgumentException("The PTY socket path must be a normalized absolute file path.", nameof(socketPath));
        }

        if (OperatingSystem.IsWindows())
        {
            // Win32 device paths, alternate streams and trimmed names are not filesystem socket paths.
            if (socketPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                socketPath[3..].Split('\\').Any(part =>
                    part.Length == 0 || part.EndsWith(' ') || part.EndsWith('.') ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    IsDeviceName(part)))
            {
                throw new ArgumentException("The PTY socket path must use ordinary local filesystem names.", nameof(socketPath));
            }
        }

        // The endpoint constructor checks the native sockaddr_un byte limit before any filesystem changes.
        _ = new UnixDomainSocketEndPoint(socketPath);
        ValidateSocketDirectory(Path.GetDirectoryName(socketPath)!);
    }

    private static bool IsDeviceName(string name)
    {
        var stem = name.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (stem.Length == 4 && stem[3] is >= '1' and <= '9' &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
    }

    public static void EnsureSocketDirectoryExistsForPath(string socketPath)
    {
        ValidateSocketPath(socketPath);
        EnsureSocketDirectoryExists(Path.GetDirectoryName(socketPath)!);
    }

    public static void EnsureSocketDirectoryExists(string socketDirectory)
    {
        ValidateSocketDirectory(socketDirectory);
        RejectDirectoryLink(socketDirectory);

        if (OperatingSystem.IsWindows())
        {
            RestrictWindowsDirectory(socketDirectory);
        }
        else
        {
            Directory.CreateDirectory(socketDirectory, DirectoryMode);
            RejectDirectoryLink(socketDirectory);
            File.SetUnixFileMode(socketDirectory, DirectoryMode);
            if (File.GetUnixFileMode(socketDirectory) != DirectoryMode)
                throw new IOException($"Could not restrict PTY socket directory '{socketDirectory}'.");
        }
    }

    internal static Socket CreateListener(string socketPath, Action<string>? restrictSocket = null)
    {
        EnsureSocketDirectoryExistsForPath(socketPath);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            // Bind exclusively: never delete a pre-existing file or another listener's endpoint.
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            (restrictSocket ?? RestrictSocket)(socketPath);
            listener.Listen(1);
            return listener;
        }
        catch
        {
            // Socket.Dispose unlinks only after a successful Bind, including permission failures.
            listener.Dispose();
            throw;
        }
    }

    private static void RestrictSocket(string socketPath)
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                GetCurrentUser(), FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(socketPath).SetAccessControl(security);
            VerifyWindowsAccess(new FileInfo(socketPath).GetAccessControl());
        }
        else
        {
            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(socketPath, mode);
            if (File.GetUnixFileMode(socketPath) != mode)
                throw new IOException($"Could not restrict PTY socket '{socketPath}'.");
        }
    }

    private static void ValidateSocketDirectory(string socketDirectory)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(socketDirectory));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string[] sharedDirectories =
        [
            Path.GetPathRoot(directory)!,
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "/tmp", "/var/tmp", "/private/tmp"
        ];
        if (Directory.GetParent(directory) is null ||
            sharedDirectories.Any(shared => !string.IsNullOrEmpty(shared) &&
                string.Equals(directory, Path.TrimEndingDirectorySeparator(Path.GetFullPath(shared)), comparison)))
        {
            throw new ArgumentException("Use a dedicated PTY socket directory, not a root, profile or shared directory.", nameof(socketDirectory));
        }
    }

    private static void RejectDirectoryLink(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.LinkTarget is not null ||
            (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0))
        {
            throw new IOException($"The PTY socket directory must not be a link or reparse point: '{path}'.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier GetCurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("The current Windows user has no SID.");
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictWindowsDirectory(string socketDirectory)
    {
        var currentUser = GetCurrentUser();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));

        var directory = new DirectoryInfo(socketDirectory);
        // The creation overload installs the DACL atomically. Existing directories are repaired below.
        directory.Create(security);
        RejectDirectoryLink(socketDirectory);
        directory.SetAccessControl(security);
        VerifyWindowsAccess(directory.GetAccessControl());
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyWindowsAccess(FileSystemSecurity security)
    {
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
        if (!security.AreAccessRulesProtected || rules.Count != 1 ||
            rules[0] is not FileSystemAccessRule rule ||
            !rule.IdentityReference.Equals(GetCurrentUser()) ||
            rule.AccessControlType != AccessControlType.Allow ||
            rule.FileSystemRights != FileSystemRights.FullControl)
        {
            throw new IOException("Could not enforce current-user-only PTY socket access.");
        }
    }

    public static void DeleteSocketFile(string? socketPath)
    {
        if (socketPath is not null)
            File.Delete(socketPath);
    }
}
