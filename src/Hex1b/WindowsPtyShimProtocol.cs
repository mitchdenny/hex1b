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

internal sealed record WindowsPtyShimLaunchRequest(
    string FileName,
    string[] Arguments,
    string? WorkingDirectory,
    Dictionary<string, string> Environment,
    int Width,
    int Height);

internal sealed record WindowsPtyShimStartedResponse(int ProcessId);

internal sealed record WindowsPtyShimResizeRequest(int Width, int Height);

internal sealed record WindowsPtyShimExitNotification(int ExitCode);

internal sealed record WindowsPtyShimErrorResponse(string Message);

internal static class WindowsPtyShimProtocol
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly WindowsPtyShimJsonContext s_jsonContext = new(s_jsonOptions);

    public static void WriteFrame(
        Stream stream,
        WindowsPtyShimFrameType type,
        ReadOnlySpan<byte> payload)
    {
        TraceProtocol($"write-sync {type} len={payload.Length}");
        var header = new byte[5];
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), payload.Length);

        stream.Write(header, 0, header.Length);
        if (!payload.IsEmpty)
        {
            stream.Write(payload);
        }

        stream.Flush();
    }

    public static async Task WriteFrameAsync(
        Stream stream,
        WindowsPtyShimFrameType type,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct)
    {
        TraceProtocol($"write-async {type} len={payload.Length}");
        var header = new byte[5];
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), payload.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        if (!payload.IsEmpty)
        {
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        }

        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static void WriteJson<T>(
        Stream stream,
        WindowsPtyShimFrameType type,
        T value)
    {
        var payload = SerializeJson(value);
        WriteFrame(stream, type, payload);
    }

    public static Task WriteJsonAsync<T>(
        Stream stream,
        WindowsPtyShimFrameType type,
        T value,
        CancellationToken ct)
    {
        var payload = SerializeJson(value);
        return WriteFrameAsync(stream, type, payload, ct);
    }

    public static byte[] SerializeJson<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, GetTypeInfo<T>());
    }

    public static (WindowsPtyShimFrameType Type, byte[] Payload)? ReadFrame(Stream stream)
    {
        var header = new byte[5];
        var bytesRead = TryReadExactly(stream, header);
        if (bytesRead == 0)
        {
            return null;
        }

        if (bytesRead != header.Length)
        {
            throw new EndOfStreamException("The PTY shim connection closed while reading a frame header.");
        }

        var type = (WindowsPtyShimFrameType)header[0];
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        if (payloadLength < 0)
        {
            throw new InvalidDataException($"Invalid PTY shim payload length: {payloadLength}.");
        }

        if (payloadLength == 0)
        {
            TraceProtocol($"read-sync {type} len=0");
            return (type, []);
        }

        var payload = new byte[payloadLength];
        bytesRead = TryReadExactly(stream, payload);
        if (bytesRead != payloadLength)
        {
            throw new EndOfStreamException("The PTY shim connection closed while reading a frame payload.");
        }

        TraceProtocol($"read-sync {type} len={payloadLength}");
        return (type, payload);
    }

    public static async Task<(WindowsPtyShimFrameType Type, byte[] Payload)?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[5];
        var bytesRead = await TryReadExactlyAsync(stream, header, ct).ConfigureAwait(false);
        if (bytesRead == 0)
        {
            return null;
        }

        if (bytesRead != header.Length)
        {
            throw new EndOfStreamException("The PTY shim connection closed while reading a frame header.");
        }

        var type = (WindowsPtyShimFrameType)header[0];
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        if (payloadLength < 0)
        {
            throw new InvalidDataException($"Invalid PTY shim payload length: {payloadLength}.");
        }

        if (payloadLength == 0)
        {
            TraceProtocol($"read-async {type} len=0");
            return (type, []);
        }

        var payload = new byte[payloadLength];
        bytesRead = await TryReadExactlyAsync(stream, payload, ct).ConfigureAwait(false);
        if (bytesRead != payloadLength)
        {
            throw new EndOfStreamException("The PTY shim connection closed while reading a frame payload.");
        }

        TraceProtocol($"read-async {type} len={payloadLength}");
        return (type, payload);
    }

    public static T ReadJson<T>(ReadOnlySpan<byte> payload)
    {
        return JsonSerializer.Deserialize(payload, GetTypeInfo<T>())
            ?? throw new InvalidDataException($"Unable to deserialize PTY shim payload as {typeof(T).Name}.");
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>()
    {
        if (typeof(T) == typeof(WindowsPtyShimLaunchRequest))
        {
            return (JsonTypeInfo<T>)(object)s_jsonContext.WindowsPtyShimLaunchRequest;
        }

        if (typeof(T) == typeof(WindowsPtyShimStartedResponse))
        {
            return (JsonTypeInfo<T>)(object)s_jsonContext.WindowsPtyShimStartedResponse;
        }

        if (typeof(T) == typeof(WindowsPtyShimResizeRequest))
        {
            return (JsonTypeInfo<T>)(object)s_jsonContext.WindowsPtyShimResizeRequest;
        }

        if (typeof(T) == typeof(WindowsPtyShimExitNotification))
        {
            return (JsonTypeInfo<T>)(object)s_jsonContext.WindowsPtyShimExitNotification;
        }

        if (typeof(T) == typeof(WindowsPtyShimErrorResponse))
        {
            return (JsonTypeInfo<T>)(object)s_jsonContext.WindowsPtyShimErrorResponse;
        }

        throw new InvalidOperationException($"Unsupported PTY shim payload type: {typeof(T).FullName}.");
    }

    private static int TryReadExactly(Stream stream, byte[] buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0)
            {
                return totalRead;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    private static async Task<int> TryReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return totalRead;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    private static void TraceProtocol(string message)
    {
        var tracePath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_PROTOCOL_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WindowsPtyShimLaunchRequest))]
[JsonSerializable(typeof(WindowsPtyShimStartedResponse))]
[JsonSerializable(typeof(WindowsPtyShimResizeRequest))]
[JsonSerializable(typeof(WindowsPtyShimExitNotification))]
[JsonSerializable(typeof(WindowsPtyShimErrorResponse))]
internal sealed partial class WindowsPtyShimJsonContext : JsonSerializerContext
{
}
