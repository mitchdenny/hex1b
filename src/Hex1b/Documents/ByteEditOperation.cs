namespace Hex1b.Documents;

/// <summary>
/// Base type for byte-level document edit operations.
/// These operate directly on the document's byte buffer, bypassing character encoding.
/// </summary>
public abstract record ByteEditOperation;
