namespace Hex1b.Documents;

/// <summary>
/// Replace bytes at a given byte offset with new bytes.
/// </summary>
public sealed record ByteReplaceOperation(int ByteOffset, int ByteCount, byte[] NewBytes) : ByteEditOperation;
