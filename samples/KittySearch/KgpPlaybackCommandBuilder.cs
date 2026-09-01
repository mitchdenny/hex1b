using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hex1b;

static class KgpPlaybackCommandBuilder
{
    private const int MaximumChunkLength = 4096;
    private const int PlacementColumns = 22;
    private const int PlacementRows = 8;

    internal static string Build(
        IReadOnlyList<KgpAnimationFrame> frames,
        int pixelWidth,
        int pixelHeight,
        bool supportsNativeAnimation)
    {
        var imageId = ComputeImageId(frames);
        return supportsNativeAnimation
            ? BuildNativeCommand(frames, pixelWidth, pixelHeight, imageId)
            : BuildClientDrivenCommand(frames, pixelWidth, pixelHeight, imageId);
    }

    private static string BuildNativeCommand(
        IReadOnlyList<KgpAnimationFrame> frames,
        int pixelWidth,
        int pixelHeight,
        uint imageId)
    {
        var sequence = new StringBuilder();
        AppendReservedRows(sequence);
        AppendChunkedTransmission(
            sequence,
            $"a=t,f=32,s={pixelWidth},v={pixelHeight},i={imageId},t=d,q=2",
            frames[0].Data,
            isAnimationFrame: false);

        foreach (var frame in frames.Skip(1))
        {
            AppendChunkedTransmission(
                sequence,
                $"a=f,f=32,s={pixelWidth},v={pixelHeight},i={imageId}," +
                $"z={frame.GapMilliseconds},X=1,q=2",
                frame.Data,
                isAnimationFrame: true);
        }

        AppendApc(
            sequence,
            $"a=p,i={imageId},c={PlacementColumns},r={PlacementRows},C=1,q=2,z=1",
            payload: null);
        AppendApc(
            sequence,
            $"a=a,i={imageId},r=1,z={frames[0].GapMilliseconds},c=1,s=3,v=1,q=2",
            payload: null);
        sequence.Append($"\\033[{PlacementRows}B");

        return WrapPrintf(sequence);
    }

    private static string BuildClientDrivenCommand(
        IReadOnlyList<KgpAnimationFrame> frames,
        int pixelWidth,
        int pixelHeight,
        uint imageId)
    {
        var cleanupSequence = new StringBuilder();
        AppendApc(cleanupSequence, $"a=d,d=I,i={imageId},q=2", payload: null);
        cleanupSequence.Append($"\\033[{PlacementRows}B");

        var reservedRows = new StringBuilder();
        AppendReservedRows(reservedRows);

        var command = new StringBuilder();
        command.Append("(cleanup() { ");
        command.Append(WrapPrintf(cleanupSequence));
        command.Append("; }; trap 'cleanup; exit 130' INT TERM; ");
        command.Append(WrapPrintf(reservedRows));
        command.Append("; while :; do ");
        foreach (var frame in frames)
        {
            var sequence = new StringBuilder();
            AppendChunkedTransmission(
                sequence,
                $"a=t,f=32,s={pixelWidth},v={pixelHeight},i={imageId},t=d,q=2",
                frame.Data,
                isAnimationFrame: false);
            AppendApc(
                sequence,
                $"a=p,i={imageId},c={PlacementColumns},r={PlacementRows},C=1,q=2,z=1",
                payload: null);

            command.Append(WrapPrintf(sequence));
            command.Append("; sleep ");
            command.Append((frame.GapMilliseconds / 1000d).ToString("0.000", CultureInfo.InvariantCulture));
            command.Append("; ");
        }
        command.Append("done)");
        return command.ToString();
    }

    private static void AppendReservedRows(StringBuilder sequence)
    {
        for (var row = 0; row < PlacementRows; row++)
            sequence.Append("\\n");
        sequence.Append($"\\033[{PlacementRows}A");
    }

    private static void AppendChunkedTransmission(
        StringBuilder sequence,
        string parameters,
        byte[] data,
        bool isAnimationFrame)
    {
        var base64 = Convert.ToBase64String(data);
        if (base64.Length <= MaximumChunkLength)
        {
            AppendApc(sequence, parameters, base64);
            return;
        }

        var offset = 0;
        var first = true;
        while (offset < base64.Length)
        {
            var length = Math.Min(MaximumChunkLength, base64.Length - offset);
            var isLast = offset + length == base64.Length;
            var chunkParameters = first
                ? $"{parameters},m=1"
                : $"{(isAnimationFrame ? "a=f," : "")}m={(isLast ? 0 : 1)}";
            AppendApc(sequence, chunkParameters, base64.Substring(offset, length));
            first = false;
            offset += length;
        }
    }

    private static void AppendApc(StringBuilder sequence, string parameters, string? payload)
    {
        sequence.Append("\\033_G");
        sequence.Append(parameters);
        if (payload is not null)
        {
            sequence.Append(';');
            sequence.Append(payload);
        }
        sequence.Append("\\033\\\\");
    }

    private static string WrapPrintf(StringBuilder sequence)
        => $"printf '%b' '{sequence}'";

    private static uint ComputeImageId(IReadOnlyList<KgpAnimationFrame> frames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> gap = stackalloc byte[sizeof(int)];
        foreach (var frame in frames)
        {
            hash.AppendData(frame.Data);
            BinaryPrimitives.WriteInt32BigEndian(gap, frame.GapMilliseconds);
            hash.AppendData(gap);
        }

        Span<byte> digest = stackalloc byte[32];
        hash.GetHashAndReset(digest);
        var imageId = BinaryPrimitives.ReadUInt32BigEndian(digest);
        return imageId == 0 ? 1u : imageId;
    }
}
