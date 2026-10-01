using System.Runtime.InteropServices;

namespace Hex1b;

[StructLayout(LayoutKind.Sequential)]
internal struct UnixPtyStartupState
{
    public int Ready;
    public int BytesRead;
    public int RecordTag;
    public int RecordError;
}
