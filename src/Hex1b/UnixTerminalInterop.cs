using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hex1b;

/// <summary>
/// Keeps platform-specific termios layouts and variadic ioctl calls in native code.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static partial class UnixTerminalInterop
{
    internal static int TermiosSize => checked((int)GetTermiosSize());

    internal static int GetTermios(int fd, byte[] buffer)
        => GetTermios(fd, buffer, (nuint)buffer.Length);

    internal static int MakeRaw(byte[] buffer, bool preserveOPost)
        => MakeRaw(buffer, (nuint)buffer.Length, preserveOPost ? 1 : 0);

    internal static int SetTermios(int fd, byte[] buffer)
        => SetTermios(fd, buffer, (nuint)buffer.Length);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_termios_size")]
    private static partial nuint GetTermiosSize();

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_termios_get", SetLastError = true)]
    private static partial int GetTermios(int fd, [Out] byte[] buffer, nuint capacity);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_termios_make_raw", SetLastError = true)]
    private static partial int MakeRaw([In, Out] byte[] buffer, nuint capacity, int preserveOPost);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_termios_set", SetLastError = true)]
    private static partial int SetTermios(int fd, byte[] buffer, nuint capacity);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_get_window_pixel_size", SetLastError = true)]
    internal static partial int GetWindowPixelSize(int fd, out int pixelWidth, out int pixelHeight);
}
