using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
[TestCategory("Unix")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class UnixTerminalInteropTests
{
    [TestInitialize]
    public void RequireUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
    }

    [TestMethod]
    public void GetTermios_NativeLayout_PreservesGuardBytes()
    {
        using var pty = new PtyPair();
        var size = UnixTerminalInterop.TermiosSize;
        Assert.IsGreaterThan(0, size);
        var buffer = Enumerable.Repeat((byte)0xa5, size + 32).ToArray();

        Assert.AreEqual(0, UnixTerminalInterop.GetTermios(pty.Slave, buffer));
        Assert.IsFalse(buffer.AsSpan(0, size).ToArray().All(b => b == 0xa5));
        Assert.IsTrue(buffer.AsSpan(size).ToArray().All(b => b == 0xa5));

        Assert.AreEqual(0, UnixTerminalInterop.MakeRaw(buffer, preserveOPost: false));
        Assert.IsTrue(buffer.AsSpan(size).ToArray().All(b => b == 0xa5));
        Assert.AreEqual(0, UnixTerminalInterop.SetTermios(pty.Slave, buffer));
        Assert.IsTrue(buffer.AsSpan(size).ToArray().All(b => b == 0xa5));
    }

    [TestMethod]
    [DataRow("get")]
    [DataRow("raw")]
    [DataRow("set")]
    public void Termios_UndersizedBuffer_RejectsWithoutMutation(string operation)
    {
        using var pty = new PtyPair();
        var original = new byte[UnixTerminalInterop.TermiosSize];
        Assert.AreEqual(0, UnixTerminalInterop.GetTermios(pty.Slave, original));
        var buffer = Enumerable.Repeat((byte)0xa5, original.Length - 1).ToArray();

        var result = operation switch
        {
            "get" => UnixTerminalInterop.GetTermios(pty.Slave, buffer),
            "raw" => UnixTerminalInterop.MakeRaw(buffer, preserveOPost: false),
            "set" => UnixTerminalInterop.SetTermios(pty.Slave, buffer),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var error = Marshal.GetLastPInvokeError();

        Assert.AreEqual(-1, result);
        Assert.AreNotEqual(0, error);
        Assert.IsTrue(buffer.All(b => b == 0xa5));
        var unchanged = new byte[original.Length];
        Assert.AreEqual(0, UnixTerminalInterop.GetTermios(pty.Slave, unchanged));
        TestSeq.AreEqual(original, unchanged);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MakeRaw_PrivatePty_PreservesInputOutputAndRestoresSettings(bool preserveOPost)
    {
        using var pty = new PtyPair();
        var original = new byte[UnixTerminalInterop.TermiosSize];
        Assert.AreEqual(0, UnixTerminalInterop.GetTermios(pty.Slave, original));
        var raw = (byte[])original.Clone();
        Assert.AreEqual(0, UnixTerminalInterop.MakeRaw(raw, preserveOPost));
        Assert.AreEqual(0, UnixTerminalInterop.SetTermios(pty.Slave, raw));

        var input = Encoding.ASCII.GetBytes("X\r\n\u0003");
        Assert.AreEqual((nint)input.Length, Write(pty.Master, input, (nuint)input.Length));
        TestSeq.AreEqual(input, ReadExactly(pty.Slave, input.Length));
        var poll = new PollFd { Fd = pty.Master, Events = 1 };
        Assert.AreEqual(0, Poll(ref poll, 1, 0), "Raw input must not be echoed.");

        Assert.AreEqual((nint)1, Write(pty.Slave, [(byte)'\n'], 1));
        var expectedOutput = Encoding.ASCII.GetBytes(preserveOPost ? "\r\n" : "\n");
        TestSeq.AreEqual(expectedOutput, ReadExactly(pty.Master, expectedOutput.Length));

        Assert.AreEqual(0, UnixTerminalInterop.SetTermios(pty.Slave, original));
        var restored = new byte[original.Length];
        Assert.AreEqual(0, UnixTerminalInterop.GetTermios(pty.Slave, restored));
        TestSeq.AreEqual(original, restored);
    }

    [TestMethod]
    public void GetWindowPixelSize_PrivatePty_ReturnsNativeDimensions()
    {
        using var pty = new PtyPair();
        Assert.AreEqual(0, UnixTerminalInterop.GetWindowPixelSize(pty.Slave, out var width, out var height));
        Assert.AreEqual(800, width);
        Assert.AreEqual(480, height);
    }

    [TestMethod]
    public void GetWindowPixelSize_InvalidDescriptor_ReturnsErrorAndZeroDimensions()
    {
        var result = UnixTerminalInterop.GetWindowPixelSize(-1, out var width, out var height);
        var error = Marshal.GetLastPInvokeError();
        Assert.AreEqual(-1, result);
        Assert.AreNotEqual(0, error);
        Assert.AreEqual(0, width);
        Assert.AreEqual(0, height);
    }

    private static byte[] ReadExactly(int fd, int count)
    {
        var result = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var poll = new PollFd { Fd = fd, Events = 1 };
            Assert.AreEqual(1, Poll(ref poll, 1, 2000), "Timed out waiting for private PTY output.");
            Assert.AreNotEqual(0, poll.ReturnedEvents & 1);
            var buffer = new byte[count - offset];
            var read = Read(fd, buffer, (nuint)buffer.Length);
            Assert.IsTrue(read > 0 && read <= buffer.Length);
            buffer.AsSpan(0, (int)read).CopyTo(result.AsSpan(offset));
            offset += (int)read;
        }
        return result;
    }

    private sealed class PtyPair : IDisposable
    {
        public int Master { get; }
        public int Slave { get; }

        public PtyPair()
        {
            var size = new WinSize { Rows = 24, Columns = 80, PixelWidth = 800, PixelHeight = 480 };
            int master, slave;
            var result = OperatingSystem.IsMacOS()
                ? OpenMacPty(out master, out slave, 0, 0, ref size)
                : OpenLinuxPty(out master, out slave, 0, 0, ref size);
            Assert.AreEqual(0, result, $"openpty failed: {Marshal.GetLastPInvokeError()}");
            Master = master;
            Slave = slave;
        }

        public void Dispose()
        {
            Close(Slave);
            Close(Master);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort PixelWidth;
        public ushort PixelHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events;
        public short ReturnedEvents;
    }

    [DllImport("libc", EntryPoint = "openpty", SetLastError = true)]
    private static extern int OpenMacPty(out int master, out int slave, nint name, nint termios, ref WinSize size);

    [DllImport("libutil.so.1", EntryPoint = "openpty", SetLastError = true)]
    private static extern int OpenLinuxPty(out int master, out int slave, nint name, nint termios, ref WinSize size);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint Read(int fd, [Out] byte[] buffer, nuint count);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint Write(int fd, byte[] buffer, nuint count);

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int Poll(ref PollFd fd, nuint count, int timeout);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int fd);
}
