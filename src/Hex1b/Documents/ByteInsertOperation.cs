namespace Hex1b.Documents;

/// <summary>
/// Insert bytes at a given byte offset.
/// </summary>
public sealed record ByteInsertOperation(int ByteOffset, byte[] NewBytes) : ByteEditOperation;
