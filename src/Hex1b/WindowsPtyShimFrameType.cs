using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hex1b;

internal enum WindowsPtyShimFrameType : byte
{
    LaunchRequest = 1,
    Started = 2,
    Output = 3,
    Input = 4,
    Resize = 5,
    Kill = 6,
    Exit = 7,
    Error = 8,
    Shutdown = 9
}
