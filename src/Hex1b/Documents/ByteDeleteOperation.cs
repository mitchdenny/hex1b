namespace Hex1b.Documents;

/// <summary>
/// Delete bytes at a given byte offset.
/// </summary>
public sealed record ByteDeleteOperation(int ByteOffset, int ByteCount) : ByteEditOperation;
