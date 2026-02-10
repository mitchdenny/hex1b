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
    private readonly List<Piece> _pieces = new();

    // Cached derived state — rebuilt after every edit
    private string _cachedText = "";
    private List<int> _lineStarts = new();
    private long _version;

    public int Length => _cachedText.Length;
    public int ByteCount { get; private set; }
    public int LineCount => _lineStarts.Count;
    public long Version => _version;

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    public Hex1bDocument(string initialText = "")
    {
        var bytes = Encoding.UTF8.GetBytes(initialText);
        _originalBuffer = bytes.AsMemory();
        ByteCount = bytes.Length;

        if (bytes.Length > 0)
        {
            _pieces.Add(new Piece(BufferSource.Original, 0, bytes.Length));
        }

        RebuildCaches();
    }

    /// <summary>
    /// Creates a document from raw bytes (not necessarily valid UTF-8).
    /// </summary>
    public Hex1bDocument(byte[] initialBytes)
    {
        _originalBuffer = initialBytes.AsMemory();
        ByteCount = initialBytes.Length;

        if (initialBytes.Length > 0)
        {
            _pieces.Add(new Piece(BufferSource.Original, 0, initialBytes.Length));
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
        var newPiece = new Piece(BufferSource.Added, addStart, bytes.Length);

        if (_pieces.Count == 0)
        {
            _pieces.Add(newPiece);
            ByteCount += bytes.Length;
            return;
        }

        var (pieceIndex, offsetInPiece) = FindPieceAt(byteOffset);

        if (pieceIndex == _pieces.Count)
        {
            _pieces.Add(newPiece);
        }
        else if (offsetInPiece == 0)
        {
            _pieces.Insert(pieceIndex, newPiece);
        }
        else if (offsetInPiece == _pieces[pieceIndex].Length)
        {
            _pieces.Insert(pieceIndex + 1, newPiece);
        }
        else
        {
            var existing = _pieces[pieceIndex];
            var left = new Piece(existing.Source, existing.Start, offsetInPiece);
            var right = new Piece(existing.Source, existing.Start + offsetInPiece, existing.Length - offsetInPiece);
            _pieces[pieceIndex] = left;
            _pieces.Insert(pieceIndex + 1, newPiece);
            _pieces.Insert(pieceIndex + 2, right);
        }

        ByteCount += bytes.Length;
    }

    private void DeleteBytesInternal(int byteOffset, int length)
    {
        if (length == 0) return;

        var end = byteOffset + length;
        var (startPiece, startInPiece) = FindPieceAt(byteOffset);
        var (endPiece, endInPiece) = FindPieceAt(end);

        var newPieces = new List<Piece>();

        if (startInPiece > 0)
        {
            var piece = _pieces[startPiece];
            newPieces.Add(new Piece(piece.Source, piece.Start, startInPiece));
        }

        if (endPiece < _pieces.Count && endInPiece < _pieces[endPiece].Length)
        {
            var piece = _pieces[endPiece];
            newPieces.Add(new Piece(piece.Source, piece.Start + endInPiece, piece.Length - endInPiece));
        }

        var removeCount = (endPiece < _pieces.Count ? endPiece + 1 : _pieces.Count) - startPiece;
        _pieces.RemoveRange(startPiece, removeCount);
        _pieces.InsertRange(startPiece, newPieces);

        ByteCount -= length;
    }

    // ── Character-level operation dispatch ───────────────────────

    private (EditOperation Applied, EditOperation Inverse) ApplyOneCharOp(EditOperation operation)
    {
        switch (operation)
        {
            case InsertOperation insert:
            {
                var byteOffset = CharOffsetToByteOffset(insert.Offset.Value);
                var textBytes = Encoding.UTF8.GetBytes(insert.Text);
                InsertBytesInternal(byteOffset, textBytes);
                var deleteInverse = new DeleteOperation(
                    new DocumentRange(insert.Offset, insert.Offset + insert.Text.Length));
                return (insert, deleteInverse);
            }

            case DeleteOperation delete:
            {
                var deletedText = GetText(delete.Range);
                var byteStart = CharOffsetToByteOffset(delete.Range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(delete.Range.End.Value);
                DeleteBytesInternal(byteStart, byteEnd - byteStart);
                var insertInverse = new InsertOperation(delete.Range.Start, deletedText);
                return (delete, insertInverse);
            }

            case ReplaceOperation replace:
            {
                var replacedText = GetText(replace.Range);
                var byteStart = CharOffsetToByteOffset(replace.Range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(replace.Range.End.Value);
                DeleteBytesInternal(byteStart, byteEnd - byteStart);
                var textBytes = Encoding.UTF8.GetBytes(replace.NewText);
                InsertBytesInternal(byteStart, textBytes);
                var replaceInverse = new ReplaceOperation(
                    new DocumentRange(replace.Range.Start, replace.Range.Start + replace.NewText.Length),
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

    // ── Piece table helpers ─────────────────────────────────────

    private (int PieceIndex, int OffsetInPiece) FindPieceAt(int byteOffset)
    {
        var current = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            if (byteOffset <= current + _pieces[i].Length)
            {
                return (i, byteOffset - current);
            }
            current += _pieces[i].Length;
        }
        return (_pieces.Count, 0);
    }

    /// <summary>
    /// Copies bytes from the piece table into the destination array.
    /// </summary>
    private void CopyBytesTo(byte[] dest, int byteOffset, int count)
    {
        var remaining = count;
        var destPos = 0;
        var current = 0;

        foreach (var piece in _pieces)
        {
            if (remaining <= 0) break;

            var pieceEnd = current + piece.Length;
            if (pieceEnd <= byteOffset)
            {
                current = pieceEnd;
                continue;
            }

            var startInPiece = Math.Max(0, byteOffset - current);
            var endInPiece = Math.Min(piece.Length, byteOffset + count - current);
            var copyCount = endInPiece - startInPiece;

            if (piece.Source == BufferSource.Original)
            {
                _originalBuffer.Span.Slice(piece.Start + startInPiece, copyCount).CopyTo(dest.AsSpan(destPos));
            }
            else
            {
                for (var i = 0; i < copyCount; i++)
                {
                    dest[destPos + i] = _addBuffer[piece.Start + startInPiece + i];
                }
            }

            destPos += copyCount;
            remaining -= copyCount;
            current = pieceEnd;
        }
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

    private enum BufferSource { Original, Added }
    private readonly record struct Piece(BufferSource Source, int Start, int Length);
}
