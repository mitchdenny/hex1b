namespace Hex1b.Documents;

/// <summary>
/// Base type for byte-level document edit operations.
/// These operate directly on the document's byte buffer, bypassing character encoding.
/// </summary>
public abstract record ByteEditOperation;

/// <summary>
/// Insert bytes at a given byte offset.
/// </summary>
public sealed record ByteInsertOperation(int ByteOffset, byte[] NewBytes) : ByteEditOperation;

/// <summary>
/// Delete bytes at a given byte offset.
/// </summary>
public sealed record ByteDeleteOperation(int ByteOffset, int ByteCount) : ByteEditOperation;

/// <summary>
/// Replace bytes at a given byte offset with new bytes.
/// </summary>
public sealed record ByteReplaceOperation(int ByteOffset, int ByteCount, byte[] NewBytes) : ByteEditOperation;
