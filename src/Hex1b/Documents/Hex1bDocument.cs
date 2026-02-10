using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// Default IHex1bDocument implementation backed by a piece table.
/// Internally byte-oriented: the piece table operates on UTF-8 byte sequences.
/// Character-level API (GetText, Length, OffsetToPosition) is derived from bytes.
/// </summary>
public sealed class Hex1bDocument : IHex1bDocument
{
    private readonly ReadOnlyMemory<byte> _originalBuffer;
    private readonly List<byte> _addBuffer = new();
    private readonly PieceTree _pieceTree = new();

    // Cached derived state — rebuilt after every edit
    private string _cachedText = "";
    private List<int> _lineStarts = new();
    private long _version;

    public int Length => _cachedText.Length;
    public int ByteCount => _pieceTree.TotalBytes;
    public int LineCount => _lineStarts.Count;
    public long Version => _version;

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    public Hex1bDocument(string initialText = "")
    {
        var bytes = Encoding.UTF8.GetBytes(initialText);
        _originalBuffer = bytes.AsMemory();

        if (bytes.Length > 0)
        {
            _pieceTree.Insert(0, PieceTree.BufferSource.Original, 0, bytes.Length);
        }

        RebuildCaches();
    }

    /// <summary>
    /// Creates a document from raw bytes (not necessarily valid UTF-8).
    /// </summary>
    public Hex1bDocument(byte[] initialBytes)
    {
        _originalBuffer = initialBytes.AsMemory();

        if (initialBytes.Length > 0)
        {
            _pieceTree.Insert(0, PieceTree.BufferSource.Original, 0, initialBytes.Length);
        }

        RebuildCaches();
    }

    public string GetText() => _cachedText;

    public string GetText(DocumentRange range)
    {
        if (range.IsEmpty) return string.Empty;
        if (range.End.Value > Length)
            throw new ArgumentOutOfRangeException(nameof(range));

        return _cachedText.Substring(range.Start.Value, range.Length);
    }

    public ReadOnlyMemory<byte> GetBytes()
    {
        var result = new byte[ByteCount];
        CopyBytesTo(result, 0, ByteCount);
        return result;
    }

    public ReadOnlyMemory<byte> GetBytes(int byteOffset, int count)
    {
        if (byteOffset < 0 || count < 0 || byteOffset + count > ByteCount)
            throw new ArgumentOutOfRangeException();

        var result = new byte[count];
        CopyBytesTo(result, byteOffset, count);
        return result;
    }

    public string GetLineText(int line)
    {
        ValidateLine(line);
        var startOffset = _lineStarts[line - 1];
        int endOffset;

        if (line < LineCount)
        {
            endOffset = _lineStarts[line];
            var text = _cachedText.Substring(startOffset, endOffset - startOffset);
            if (text.EndsWith("\r\n")) return text[..^2];
            if (text.EndsWith('\n')) return text[..^1];
            return text;
        }

        endOffset = Length;
        return _cachedText.Substring(startOffset, endOffset - startOffset);
    }

    public int GetLineLength(int line)
    {
        ValidateLine(line);
        return GetLineText(line).Length;
    }

    public DocumentPosition OffsetToPosition(DocumentOffset offset)
    {
        if (offset.Value > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var line = 1;
        for (var i = _lineStarts.Count - 1; i >= 0; i--)
        {
            if (_lineStarts[i] <= offset.Value)
            {
                line = i + 1;
                break;
            }
        }

        var column = offset.Value - _lineStarts[line - 1] + 1;
        return new DocumentPosition(line, column);
    }

    public DocumentOffset PositionToOffset(DocumentPosition position)
    {
        ValidateLine(position.Line);
        var lineStart = _lineStarts[position.Line - 1];
        return new DocumentOffset(lineStart + position.Column - 1);
    }

    // ── Character-level editing ─────────────────────────────────

    public EditResult Apply(EditOperation operation, string? source = null)
    {
        return Apply([operation], source);
    }

    public EditResult Apply(IReadOnlyList<EditOperation> operations, string? source = null)
    {
        var previousVersion = _version;
        var applied = new List<EditOperation>(operations.Count);
        var inverse = new List<EditOperation>(operations.Count);

        foreach (var op in operations)
        {
            var (appliedOp, inverseOp) = ApplyOneCharOp(op);
            applied.Add(appliedOp);
            inverse.Add(inverseOp);
        }

        _version++;
        RebuildCaches();

        var result = new EditResult(previousVersion, _version, applied, inverse);
        Changed?.Invoke(this, new DocumentChangedEventArgs(
            _version, previousVersion, applied, inverse, source));

        return result;
    }

    // ── Byte-level editing ──────────────────────────────────────

    public EditResult ApplyBytes(ByteEditOperation operation, string? source = null)
    {
        var previousVersion = _version;

        // Convert to char-level inverse for undo compatibility
        var textBefore = _cachedText;

        switch (operation)
        {
            case ByteInsertOperation insert:
                InsertBytesInternal(insert.ByteOffset, insert.NewBytes);
                break;
            case ByteDeleteOperation delete:
                DeleteBytesInternal(delete.ByteOffset, delete.ByteCount);
                break;
            case ByteReplaceOperation replace:
                DeleteBytesInternal(replace.ByteOffset, replace.ByteCount);
                InsertBytesInternal(replace.ByteOffset, replace.NewBytes);
                break;
            default:
                throw new ArgumentException($"Unknown byte operation: {operation.GetType()}");
        }

        _version++;
        RebuildCaches();

        // Build character-level edit operations for undo/event compatibility
        var textAfter = _cachedText;
        var (charOp, charInverse) = DiffToEditOps(textBefore, textAfter);

        var result = new EditResult(previousVersion, _version, [charOp], [charInverse]);
        Changed?.Invoke(this, new DocumentChangedEventArgs(
            _version, previousVersion, [charOp], [charInverse], source));

        return result;
    }

    // ── Internal byte operations ────────────────────────────────

    private void InsertBytesInternal(int byteOffset, byte[] bytes)
    {
        if (bytes.Length == 0) return;

        var addStart = _addBuffer.Count;
        _addBuffer.AddRange(bytes);
        _pieceTree.Insert(byteOffset, PieceTree.BufferSource.Added, addStart, bytes.Length);
    }

    private void DeleteBytesInternal(int byteOffset, int length)
    {
        if (length == 0) return;
        _pieceTree.Delete(byteOffset, length);
    }

    // ── Character-level operation dispatch ───────────────────────

    private (EditOperation Applied, EditOperation Inverse) ApplyOneCharOp(EditOperation operation)
    {
        switch (operation)
        {
            case InsertOperation insert:
            {
                var clampedOffset = Math.Min(insert.Offset.Value, Length);
                var byteOffset = CharOffsetToByteOffset(clampedOffset);
                var textBytes = Encoding.UTF8.GetBytes(insert.Text);
                InsertBytesInternal(byteOffset, textBytes);
                var deleteInverse = new DeleteOperation(
                    new DocumentRange(new DocumentOffset(clampedOffset), new DocumentOffset(clampedOffset + insert.Text.Length)));
                return (insert, deleteInverse);
            }

            case DeleteOperation delete:
            {
                // Clamp range to current document bounds (can be stale from undo after byte ops)
                var clampedEnd = Math.Min(delete.Range.End.Value, Length);
                var clampedStart = Math.Min(delete.Range.Start.Value, clampedEnd);
                var range = new DocumentRange(new DocumentOffset(clampedStart), new DocumentOffset(clampedEnd));
                if (range.IsEmpty) return (delete, new InsertOperation(range.Start, ""));

                var deletedText = GetText(range);
                var byteStart = CharOffsetToByteOffset(range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(range.End.Value);
                DeleteBytesInternal(byteStart, byteEnd - byteStart);
                var insertInverse = new InsertOperation(range.Start, deletedText);
                return (delete, insertInverse);
            }

            case ReplaceOperation replace:
            {
                // Clamp range to current document bounds
                var clampedEnd = Math.Min(replace.Range.End.Value, Length);
                var clampedStart = Math.Min(replace.Range.Start.Value, clampedEnd);
                var range = new DocumentRange(new DocumentOffset(clampedStart), new DocumentOffset(clampedEnd));

                var replacedText = range.IsEmpty ? "" : GetText(range);
                var byteStart = CharOffsetToByteOffset(range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(range.End.Value);
                if (byteEnd > byteStart) DeleteBytesInternal(byteStart, byteEnd - byteStart);
                var textBytes = Encoding.UTF8.GetBytes(replace.NewText);
                InsertBytesInternal(byteStart, textBytes);
                var replaceInverse = new ReplaceOperation(
                    new DocumentRange(range.Start, range.Start + replace.NewText.Length),
                    replacedText);
                return (replace, replaceInverse);
            }

            default:
                throw new ArgumentException($"Unknown operation type: {operation.GetType()}", nameof(operation));
        }
    }

    /// <summary>
    /// Converts a character offset in the current cached text to a byte offset.
    /// </summary>
    private int CharOffsetToByteOffset(int charOffset)
    {
        if (charOffset <= 0) return 0;
        if (charOffset >= _cachedText.Length) return ByteCount;
        return Encoding.UTF8.GetByteCount(_cachedText.AsSpan(0, charOffset));
    }

    // ── Piece tree helpers ────────────────────────────────────────

    /// <summary>
    /// Copies bytes from the piece tree into the destination array.
    /// </summary>
    private void CopyBytesTo(byte[] dest, int byteOffset, int count)
    {
        var remaining = count;
        var destPos = 0;
        var current = 0;

        _pieceTree.InOrderTraversal((source, start, length) =>
        {
            if (remaining <= 0) return;

            var pieceEnd = current + length;
            if (pieceEnd <= byteOffset)
            {
                current = pieceEnd;
                return;
            }

            var startInPiece = Math.Max(0, byteOffset - current);
            var endInPiece = Math.Min(length, byteOffset + count - current);
            var copyCount = endInPiece - startInPiece;

            if (source == PieceTree.BufferSource.Original)
            {
                _originalBuffer.Span.Slice(start + startInPiece, copyCount).CopyTo(dest.AsSpan(destPos));
            }
            else
            {
                for (var i = 0; i < copyCount; i++)
                {
                    dest[destPos + i] = _addBuffer[start + startInPiece + i];
                }
            }

            destPos += copyCount;
            remaining -= copyCount;
            current = pieceEnd;
        });
    }

    /// <summary>
    /// Assembles all bytes from the piece table.
    /// </summary>
    private byte[] AssembleBytes()
    {
        var result = new byte[ByteCount];
        CopyBytesTo(result, 0, ByteCount);
        return result;
    }

    /// <summary>
    /// Rebuilds all cached derived state from bytes: text, line starts.
    /// </summary>
    private void RebuildCaches()
    {
        var bytes = AssembleBytes();
        _cachedText = Encoding.UTF8.GetString(bytes);
        RebuildLineStarts();
    }

    private void RebuildLineStarts()
    {
        _lineStarts = [0];
        for (var i = 0; i < _cachedText.Length; i++)
        {
            if (_cachedText[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    /// <summary>
    /// Produces character-level edit operations by diffing before/after text.
    /// Used by ApplyBytes to generate undo-compatible operations.
    /// </summary>
    private static (EditOperation Op, EditOperation Inverse) DiffToEditOps(string before, string after)
    {
        // Find common prefix
        var prefixLen = 0;
        var minLen = Math.Min(before.Length, after.Length);
        while (prefixLen < minLen && before[prefixLen] == after[prefixLen])
            prefixLen++;

        // Find common suffix (not overlapping with prefix)
        var suffixLen = 0;
        while (suffixLen < minLen - prefixLen &&
               before[before.Length - 1 - suffixLen] == after[after.Length - 1 - suffixLen])
            suffixLen++;

        var deletedLen = before.Length - prefixLen - suffixLen;
        var insertedLen = after.Length - prefixLen - suffixLen;
        var deletedText = before.Substring(prefixLen, deletedLen);
        var insertedText = after.Substring(prefixLen, insertedLen);

        if (deletedLen == 0 && insertedLen > 0)
        {
            var op = new InsertOperation(new DocumentOffset(prefixLen), insertedText);
            var inv = new DeleteOperation(new DocumentRange(
                new DocumentOffset(prefixLen), new DocumentOffset(prefixLen + insertedLen)));
            return (op, inv);
        }

        if (deletedLen > 0 && insertedLen == 0)
        {
            var op = new DeleteOperation(new DocumentRange(
                new DocumentOffset(prefixLen), new DocumentOffset(prefixLen + deletedLen)));
            var inv = new InsertOperation(new DocumentOffset(prefixLen), deletedText);
            return (op, inv);
        }

        // Replace
        var replaceOp = new ReplaceOperation(
            new DocumentRange(new DocumentOffset(prefixLen), new DocumentOffset(prefixLen + deletedLen)),
            insertedText);
        var replaceInv = new ReplaceOperation(
            new DocumentRange(new DocumentOffset(prefixLen), new DocumentOffset(prefixLen + insertedLen)),
            deletedText);
        return (replaceOp, replaceInv);
    }

    private void ValidateLine(int line)
    {
        if (line < 1 || line > LineCount)
            throw new ArgumentOutOfRangeException(nameof(line), $"Line {line} is out of range [1..{LineCount}].");
    }

    /// <summary>Validates internal piece tree consistency. Throws if corrupt.</summary>
    internal void VerifyIntegrity() => _pieceTree.VerifyIntegrity();

    public DocumentDiagnosticInfo GetDiagnosticInfo()
    {
        const int maxPreviewBytes = 64;
        var pieceList = _pieceTree.ToList();
        var pieces = new List<PieceDiagnosticInfo>(pieceList.Count);

        for (var i = 0; i < pieceList.Count; i++)
        {
            var (source, start, length) = pieceList[i];
            var previewLen = Math.Min(length, maxPreviewBytes);
            var preview = new byte[previewLen];

            if (source == PieceTree.BufferSource.Original)
            {
                _originalBuffer.Span.Slice(start, previewLen).CopyTo(preview);
            }
            else
            {
                for (var j = 0; j < previewLen; j++)
                    preview[j] = _addBuffer[start + j];
            }

            pieces.Add(new PieceDiagnosticInfo
            {
                Index = i,
                Source = source == PieceTree.BufferSource.Original ? "Original" : "Added",
                Start = start,
                Length = length,
                PreviewBytes = preview,
                PreviewText = Encoding.UTF8.GetString(preview)
            });
        }

        return new DocumentDiagnosticInfo
        {
            Version = _version,
            CharCount = Length,
            ByteCount = ByteCount,
            LineCount = LineCount,
            OriginalBufferSize = _originalBuffer.Length,
            AddBufferSize = _addBuffer.Count,
            Pieces = pieces,
            PieceCount = _pieceTree.Count,
            TreeRoot = _pieceTree.GetDiagnosticRoot()
        };
    }
}
