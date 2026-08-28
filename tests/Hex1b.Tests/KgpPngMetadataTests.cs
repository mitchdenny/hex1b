using System.Buffers.Binary;

namespace Hex1b.Tests;

[TestClass]
public class KgpPngMetadataTests
{
    [TestMethod]
    public void TryReadDimensions_ValidIhdr_ReturnsIntrinsicDimensions()
    {
        var png = CreatePngHeader(100, 80);

        var result = KgpPngMetadata.TryReadDimensions(
            png,
            out var width,
            out var height);

        Assert.IsTrue(result);
        Assert.AreEqual(100u, width);
        Assert.AreEqual(80u, height);
    }

    [TestMethod]
    [DataRow("signature")]
    [DataRow("truncated")]
    [DataRow("length")]
    [DataRow("type")]
    [DataRow("zero-width")]
    [DataRow("zero-height")]
    [DataRow("overflow-width")]
    [DataRow("overflow-height")]
    public void TryReadDimensions_InvalidIhdr_RemainsUnresolved(
        string scenario)
    {
        var png = CreatePngHeader(100, 80);
        png = scenario switch
        {
            "signature" => Mutate(png, 0, 0),
            "truncated" => png[..32],
            "length" => MutateUInt32(png, 8, 12),
            "type" => Mutate(png, 12, (byte)'X'),
            "zero-width" => MutateUInt32(png, 16, 0),
            "zero-height" => MutateUInt32(png, 20, 0),
            "overflow-width" => MutateUInt32(png, 16, 0x80000000),
            "overflow-height" => MutateUInt32(png, 20, 0x80000000),
            _ => throw new InvalidOperationException(scenario),
        };

        Assert.IsFalse(KgpPngMetadata.TryReadDimensions(
            png,
            out var width,
            out var height));
        Assert.AreEqual(0u, width);
        Assert.AreEqual(0u, height);
    }

    private static byte[] CreatePngHeader(uint width, uint height)
    {
        var png = new byte[33];
        new byte[]
        {
            0x89, (byte)'P', (byte)'N', (byte)'G',
            0x0D, 0x0A, 0x1A, 0x0A,
        }.CopyTo(png, 0);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(8), 13);
        "IHDR"u8.CopyTo(png.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), height);
        png[24] = 8;
        png[25] = 6;
        return png;
    }

    private static byte[] Mutate(byte[] source, int offset, byte value)
    {
        var result = source.ToArray();
        result[offset] = value;
        return result;
    }

    private static byte[] MutateUInt32(
        byte[] source,
        int offset,
        uint value)
    {
        var result = source.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(
            result.AsSpan(offset),
            value);
        return result;
    }
}
