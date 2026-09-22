using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
[DoNotParallelize]
public class WindowsPtySocketPathTests
{
    [TestMethod]
    public void CreateSocketPath_DefaultAndExplicitPaths_PreserveSelectionRules()
    {
        Assert.IsNull(new Hex1bTerminalProcessOptions().WindowsPtyProxySocketPath);
        using var directory = new SocketDirectory();
        var original = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR");
        try
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", directory.Path);
            var first = WindowsPtySocketPaths.CreateSocketPath();
            var second = WindowsPtySocketPaths.CreateSocketPath();
            Assert.AreEqual(directory.Path, System.IO.Path.GetDirectoryName(first));
            Assert.StartsWith("hex1bpty-", System.IO.Path.GetFileName(first));
            Assert.EndsWith(".socket", first);
            Assert.AreNotEqual(first, second);

            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", "invalid\0directory");
            var exact = System.IO.Path.Combine(directory.Path, "exact.socket");
            Assert.AreEqual(exact, WindowsPtySocketPaths.CreateSocketPath(exact));
            Assert.IsFalse(File.Exists(exact));

            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", null);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Assert.AreEqual(System.IO.Path.Combine(home, ".hex1b", "hex1bpty"),
                WindowsPtySocketPaths.GetSocketDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_PTY_SHIM_SOCKET_DIR", original);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("relative.socket")]
    [DataRow("bad\0path")]
    public void CreateSocketPath_InvalidPath_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => WindowsPtySocketPaths.CreateSocketPath(path));
    }

    [TestMethod]
    public void CreateSocketPath_NonCanonicalOrLongPaths_FailsBeforeDirectoryCreation()
    {
        using var directory = new SocketDirectory();
        var missing = System.IO.Path.Combine(directory.Path, "missing");
        string[] paths =
        [
            missing + System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.Combine(missing, "..", "s"),
            System.IO.Path.Combine(missing, new string('s', 108)),
            System.IO.Path.Combine(missing, new string('\u00e9', 54))
        ];
        foreach (var path in paths)
            Assert.Throws<ArgumentException>(() => WindowsPtySocketPaths.CreateSocketPath(path));
        Assert.IsFalse(Directory.Exists(missing));
    }

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow(@"C:\sockets\NUL")]
    [DataRow(@"C:\sockets\COM1.socket")]
    [DataRow(@"C:\sockets\s:stream")]
    [DataRow(@"C:\sockets\s.")]
    [DataRow(@"C:\sockets\s ")]
    [DataRow(@"\\server\share\sockets\s")]
    [DataRow(@"\\?\C:\sockets\s")]
    public void CreateSocketPath_WindowsSpecialNames_Throws(string path)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Requires Windows path validation.");
        Assert.Throws<ArgumentException>(() => WindowsPtySocketPaths.CreateSocketPath(path));
    }

    [TestMethod]
    public void EnsureSocketDirectoryExists_SharedDirectories_AreRejected()
    {
        var paths = new[]
        {
            System.IO.Path.GetPathRoot(Environment.CurrentDirectory)!,
            System.IO.Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        foreach (var path in paths.Where(path => !string.IsNullOrEmpty(path)))
            Assert.Throws<ArgumentException>(() => WindowsPtySocketPaths.EnsureSocketDirectoryExists(path));
    }

    [TestMethod]
    public void EnsureSocketDirectoryExists_FinalDirectoryLink_DoesNotChangeTarget()
    {
        using var directory = new SocketDirectory();
        var target = System.IO.Path.Combine(directory.Path, "target");
        var link = System.IO.Path.Combine(directory.Path, "link");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Creating symbolic links requires Developer Mode or elevation.");
        }

        var before = OperatingSystem.IsWindows()
            ? new DirectoryInfo(target).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access)
            : File.GetUnixFileMode(target).ToString();
        Assert.ThrowsExactly<IOException>(() => WindowsPtySocketPaths.EnsureSocketDirectoryExists(link));
        var after = OperatingSystem.IsWindows()
            ? new DirectoryInfo(target).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access)
            : File.GetUnixFileMode(target).ToString();
        Assert.AreEqual(before, after);
        Directory.Delete(link);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnsureSocketDirectoryExists_CreatesOrRepairsRestrictivePermissions(bool existing)
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "sockets");
        if (existing)
        {
            Directory.CreateDirectory(path);
            if (OperatingSystem.IsWindows())
            {
                var security = new DirectoryInfo(path).GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(path).SetAccessControl(security);
            }
            else
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        WindowsPtySocketPaths.EnsureSocketDirectoryExists(path);
        if (OperatingSystem.IsWindows())
        {
            var rule = AssertPrivateAccess(new DirectoryInfo(path).GetAccessControl());
            Assert.AreEqual(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, rule.InheritanceFlags);
        }
        else
        {
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(path));
        }
    }

    [TestMethod]
    public async Task CreateListener_RestrictsEndpoint_AllowsOwnerAndRebindsAfterDisposal()
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        for (var iteration = 0; iteration < 2; iteration++)
        {
            using (var listener = WindowsPtySocketPaths.CreateListener(path))
            {
                if (OperatingSystem.IsWindows())
                    AssertPrivateAccess(new FileInfo(path).GetAccessControl());
                else
                    Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await client.ConnectAsync(new UnixDomainSocketEndPoint(path), timeout.Token);
                using var accepted = await listener.AcceptAsync(timeout.Token);
                await client.SendAsync(new byte[] { 42 }, SocketFlags.None, timeout.Token);
                var buffer = new byte[1];
                Assert.AreEqual(1, await accepted.ReceiveAsync(buffer, SocketFlags.None, timeout.Token));
                Assert.AreEqual(42, buffer[0]);
            }
            Assert.IsFalse(System.IO.Path.Exists(path));
        }
    }

    [TestMethod]
    public async Task CreateListener_Collision_DoesNotDeleteFileOrLiveEndpoint()
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        File.WriteAllText(path, "keep");
        Assert.ThrowsExactly<IOException>(() => WindowsPtySocketPaths.CreateSocketPath(path));
        Assert.ThrowsExactly<SocketException>(() => WindowsPtySocketPaths.CreateListener(path));
        Assert.AreEqual("keep", File.ReadAllText(path));
        File.Delete(path);

        using var listener = WindowsPtySocketPaths.CreateListener(path);
        Assert.ThrowsExactly<IOException>(() => WindowsPtySocketPaths.CreateSocketPath(path));
        Assert.ThrowsExactly<SocketException>(() => WindowsPtySocketPaths.CreateListener(path));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(path), timeout.Token);
        using var accepted = await listener.AcceptAsync(timeout.Token);
        Assert.IsTrue(accepted.Connected);
    }

    [TestMethod]
    public void CreateListener_PermissionFailure_DoesNotListenAndRemovesBoundEndpoint()
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        var failure = new UnauthorizedAccessException("Controlled permission failure.");
        var actual = Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            WindowsPtySocketPaths.CreateListener(path, boundPath =>
            {
                Assert.AreEqual(path, boundPath);
                Assert.IsTrue(System.IO.Path.Exists(path));
                using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                Assert.ThrowsExactly<SocketException>(() => client.Connect(new UnixDomainSocketEndPoint(path)));
                throw failure;
            }));
        Assert.AreSame(failure, actual);
        Assert.IsFalse(System.IO.Path.Exists(path));
        using var listener = WindowsPtySocketPaths.CreateListener(path);
    }

    [TestMethod]
    public void CreateListener_DirectoryCreationDenied_FailsWithoutBinding()
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "denied", "s");
        if (OperatingSystem.IsWindows())
        {
            var info = new DirectoryInfo(directory.Path);
            var security = info.GetAccessControl();
            using var identity = WindowsIdentity.GetCurrent();
            var denyWrite = new FileSystemAccessRule(identity.User!,
                FileSystemRights.Write, AccessControlType.Deny);
            security.AddAccessRule(denyWrite);
            info.SetAccessControl(security);
            try
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!));
                Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                {
                    using var listener = WindowsPtySocketPaths.CreateListener(path);
                });
                Assert.IsFalse(System.IO.Path.Exists(path));
            }
            finally
            {
                // Persist only writes modified ACL sections; reusing an untouched snapshot does nothing.
                security.RemoveAccessRuleSpecific(denyWrite);
                info.SetAccessControl(security);
            }
        }
        else
        {
            File.SetUnixFileMode(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                if (GetEffectiveUserId() == 0)
                    Assert.Inconclusive("Root can bypass directory permissions.");
                Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                {
                    using var listener = WindowsPtySocketPaths.CreateListener(path);
                });
                Assert.IsFalse(System.IO.Path.Exists(path));
            }
            finally
            {
                File.SetUnixFileMode(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    [TestMethod]
    public async Task CreateListener_CanceledAccept_CleansUpAndAllowsRebind()
    {
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        using (var listener = WindowsPtySocketPaths.CreateListener(path))
        {
            using var cancellation = new CancellationTokenSource();
            var accepting = listener.AcceptAsync(cancellation.Token).AsTask();
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => accepting);
        }
        Assert.IsFalse(System.IO.Path.Exists(path));
        using var replacement = WindowsPtySocketPaths.CreateListener(path);
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_ExplicitSocketPath_IsSnapshottedAndUsedByHelper()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires Windows ConPTY and hex1bpty.exe.");
            return;
        }
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "exact.socket");
        Hex1bTerminalProcessOptions? captured = null;
        var builder = Hex1bTerminal.CreateBuilder().WithPtyProcess(options =>
        {
            captured = options;
            options.FileName = "cmd.exe";
            options.Arguments = ["/q", "/d", "/k"];
            options.WindowsPtyProxySocketPath = path;
        }).WithHeadless();
        captured!.WindowsPtyProxySocketPath = "invalid-after-configuration";
        captured.WindowsPtyMode = WindowsPtyMode.Direct;
        captured.WindowsPtyHostPath = "missing-after-configuration";

        await using (var terminal = builder.Build())
        {
            var process = TestSeq.IsType<Hex1bTerminalChildProcess>(terminal.Workload);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.StartAsync(timeout.Token);
            Assert.IsTrue(File.Exists(path));
            AssertPrivateAccess(new FileInfo(path).GetAccessControl());
        }
        Assert.IsFalse(System.IO.Path.Exists(path));
        Assert.IsFalse(System.IO.Path.Exists(path + ".lock"));
        using var rebound = WindowsPtySocketPaths.CreateListener(path);
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_ExistingFileOrReservation_FailsWithoutDeletingIt()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires Windows ConPTY and hex1bpty.exe.");
            return;
        }
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        foreach (var occupied in new[] { path, path + ".lock" })
        {
            File.WriteAllText(occupied, "keep");
            await using (var handle = new WindowsProxyPtyHandle(windowsPtyProxySocketPath: path))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                    handle.StartAsync("cmd.exe", ["/d", "/c", "exit 0"], null, new(), 80, 24, timeout.Token));
                Assert.IsInstanceOfType<IOException>(error.InnerException);
            }
            Assert.AreEqual("keep", File.ReadAllText(occupied));
            File.Delete(occupied);
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WindowsProxyPtyHandle_Cancellation_ReleasesExplicitPath(bool beforeStart)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires Windows ConPTY and hex1bpty.exe.");
            return;
        }
        using var directory = new SocketDirectory();
        var path = System.IO.Path.Combine(directory.Path, "s");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using (var handle = new WindowsProxyPtyHandle(windowsPtyProxySocketPath: path))
        {
            if (beforeStart)
            {
                cancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    handle.StartAsync("cmd.exe", ["/q", "/d", "/k"], null, new(), 80, 24, cancellation.Token));
            }
            else
            {
                await handle.StartAsync("cmd.exe", ["/q", "/d", "/k"], null, new(), 80, 24, cancellation.Token);
                Assert.IsTrue(File.Exists(path));
                cancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => handle.WaitForExitAsync(cancellation.Token));
            }
        }
        Assert.IsFalse(System.IO.Path.Exists(path));
        Assert.IsFalse(System.IO.Path.Exists(path + ".lock"));
        using var rebound = WindowsPtySocketPaths.CreateListener(path);
    }

    [TestMethod]
    [DataRow(WindowsPtyMode.Direct)]
    [DataRow(WindowsPtyMode.RequireProxy)]
    public async Task WithPtyProcess_Unix_IgnoresWindowsProxySocketPath(WindowsPtyMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires a Unix PTY.");
            return;
        }
        await using var terminal = Hex1bTerminal.CreateBuilder().WithPtyProcess(options =>
        {
            options.FileName = "/bin/echo";
            options.Arguments = ["hello"];
            options.WindowsPtyMode = mode;
            options.WindowsPtyProxySocketPath = "invalid\0ignored";
        }).WithHeadless().Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Assert.AreEqual(0, await terminal.RunAsync(timeout.Token));
    }

    [TestMethod]
    [DataRow(WindowsPtyMode.Direct, "")]
    [DataRow(WindowsPtyMode.Direct, " ")]
    [DataRow(WindowsPtyMode.Direct, "session.socket")]
    [DataRow((WindowsPtyMode)42, "session.socket")]
    public async Task WindowsProxyPtyHandle_NonProxyModeWithSocketPath_RejectsBeforeLaunch(
        WindowsPtyMode mode, string path)
    {
        await using var handle = new WindowsProxyPtyHandle(mode, windowsPtyProxySocketPath: path);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            handle.StartAsync("not-an-executable", [], null, new(), 80, 24, CancellationToken.None));
        Assert.Contains(nameof(Hex1bTerminalProcessOptions.WindowsPtyProxySocketPath), error.Message);
        Assert.Contains("WindowsPtyMode.RequireProxy", error.Message);
        Assert.AreEqual(-1, handle.ProcessId);
    }

    [TestMethod]
    [TestCategory("Windows")]
    public async Task WithPtyProcess_DirectWithProxySocketPath_FailsAtStartupWithoutCreatingDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires Windows PTY backend selection.");
            return;
        }
        using var directory = new SocketDirectory();
        var missing = System.IO.Path.Combine(directory.Path, "missing");
        await using var terminal = Hex1bTerminal.CreateBuilder().WithPtyProcess(options =>
        {
            options.FileName = "cmd.exe";
            options.Arguments = ["/d", "/c", "exit 0"];
            options.WindowsPtyMode = WindowsPtyMode.Direct;
            options.WindowsPtyProxySocketPath = System.IO.Path.Combine(missing, "s");
        }).WithHeadless().Build();

        var process = TestSeq.IsType<Hex1bTerminalChildProcess>(terminal.Workload);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync());
        Assert.Contains("WindowsPtyProxySocketPath requires WindowsPtyMode.RequireProxy", error.Message);
        Assert.IsFalse(process.HasStarted);
        Assert.AreEqual(-1, process.ProcessId);
        Assert.IsFalse(Directory.Exists(missing));
    }

    [SupportedOSPlatform("windows")]
    private static FileSystemAccessRule AssertPrivateAccess(FileSystemSecurity security)
    {
        Assert.IsTrue(security.AreAccessRulesProtected);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        var rule = TestSeq.Single(rules);
        using var identity = WindowsIdentity.GetCurrent();
        Assert.AreEqual(identity.User, rule.IdentityReference);
        Assert.AreEqual(AccessControlType.Allow, rule.AccessControlType);
        Assert.AreEqual(FileSystemRights.FullControl, rule.FileSystemRights);
        Assert.IsFalse(rule.IsInherited);
        return rule;
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    private sealed class SocketDirectory : IDisposable
    {
        public string Path { get; }

        public SocketDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                Path = Directory.CreateTempSubdirectory("h1b-").FullName;
            }
            else
            {
                // mkdtemp creates a private 0700 directory atomically without changing TMPDIR.
                var template = Encoding.UTF8.GetBytes("/tmp/h1b-XXXXXX\0");
                if (Mkdtemp(template) == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                Path = Encoding.UTF8.GetString(template.AsSpan(0, template.Length - 1));
            }
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);

        [DllImport("libc", EntryPoint = "mkdtemp", SetLastError = true)]
        private static extern IntPtr Mkdtemp([In, Out] byte[] template);
    }
}
