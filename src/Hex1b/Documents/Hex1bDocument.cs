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

    // Cached derived state — lazily rebuilt after edits, or fully rebuilt after batch
    private string _cachedText = "";
    private byte[] _cachedBytes = [];
    private Utf8ByteMap? _cachedByteMap;
    private List<int> _lineStartChars = new();  // char offsets of line starts
    private List<int> _lineStartBytes = new();  // byte offsets of line starts (parallel)
    private bool _textDirty;
    private bool _bytesDirty;
    private long _version;
    private int _batchDepth;
    private int _batchLengthDelta; // Tracks character-length changes during batch
    private long _batchVersionStart; // Version before batch began
    private List<EditOperation>? _batchApplied; // Accumulated ops during batch
    private List<EditOperation>? _batchInverse;

    public int Length
    {
        get
        {
            if (_batchDepth > 0)
                return Math.Max(0, _cachedText.Length + _batchLengthDelta);
            // Outside batch: line starts are authoritative for Length calculation
            // Length = start of last line + length of last line content
            // But we need the actual char count. Use piece tree byte count + decode if dirty.
            if (_textDirty)
                EnsureTextValid();
            return _cachedText.Length;
        }
    }
    public int ByteCount => _pieceTree.TotalBytes;
    public int LineCount => _lineStartChars.Count;
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

    public string GetText()
    {
        EnsureTextValid();
        return _cachedText;
    }

    // ── Batch editing ───────────────────────────────────────────

    /// <summary>
    /// Begins a batch of edits. While a batch is open, <see cref="RebuildCaches"/>
    /// and <see cref="Changed"/> events are deferred until <see cref="EndBatch"/> is called.
    /// Batches may be nested; only the outermost <see cref="EndBatch"/> triggers the rebuild.
    /// <para>
    /// This is critical for multi-cursor edits: without batching, each cursor's edit
    /// triggers a full O(n) cache rebuild (assemble bytes + UTF-8 decode + line scan).
    /// With batching, all piece-tree mutations happen first, then a single O(n) rebuild.
    /// </para>
    /// </summary>
    public void BeginBatch()
    {
        if (_batchDepth++ == 0)
        {
            // Ensure caches are valid before batch starts — batch edits
            // read from _cachedText for char→byte conversion and deleted text.
            EnsureTextValid();
            _batchVersionStart = _version;
            _batchApplied = new List<EditOperation>();
            _batchInverse = new List<EditOperation>();
        }
    }

    /// <summary>
    /// Ends a batch of edits. When the outermost batch ends, rebuilds all caches
    /// once and fires a single <see cref="Changed"/> event.
    /// </summary>
    public void EndBatch()
    {
        if (_batchDepth <= 0) return;
        if (--_batchDepth == 0)
        {
            var applied = _batchApplied ?? [];
            var inverse = _batchInverse ?? [];
            var previousVersion = _batchVersionStart;

            _batchLengthDelta = 0;
            _batchApplied = null;
            _batchInverse = null;
            RebuildCaches();

            if (applied.Count > 0)
            {
                Changed?.Invoke(this, new DocumentChangedEventArgs(
                    _version, previousVersion, applied, inverse, source: null));
            }
        }
    }

    public string GetText(DocumentRange range)
    {
        if (range.IsEmpty) return string.Empty;
        EnsureTextValid();
        if (range.End.Value > _cachedText.Length)
            throw new ArgumentOutOfRangeException(nameof(range));

        return _cachedText.Substring(range.Start.Value, range.Length);
    }

    public ReadOnlyMemory<byte> GetBytes()
    {
        EnsureBytesValid();
        return _cachedBytes;
    }

    public ReadOnlyMemory<byte> GetBytes(int byteOffset, int count)
    {
        if (byteOffset < 0 || count < 0 || byteOffset + count > ByteCount)
            throw new ArgumentOutOfRangeException();

        EnsureBytesValid();
        return _cachedBytes.AsMemory(byteOffset, count);
    }

    /// <summary>
    /// Returns a cached byte↔char mapping. The map is lazily rebuilt when the
    /// document changes and reused across calls within the same document version.
    /// </summary>
    public Utf8ByteMap GetByteMap()
    {
        EnsureBytesValid();
        return _cachedByteMap ??= new Utf8ByteMap(_cachedBytes);
    }

    public string GetLineText(int line)
    {
        ValidateLine(line);
        var byteStart = _lineStartBytes[line - 1];
        int byteEnd;

        if (line < LineCount)
        {
            byteEnd = _lineStartBytes[line];
            var text = ReadTextFromPieces(byteStart, byteEnd - byteStart);
            if (text.EndsWith("\r\n")) return text[..^2];
            if (text.EndsWith('\n')) return text[..^1];
            return text;
        }

        byteEnd = ByteCount;
        return ReadTextFromPieces(byteStart, byteEnd - byteStart);
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

        // Binary search: _lineStartChars is sorted ascending
        var idx = _lineStartChars.BinarySearch(offset.Value);
        int line;
        if (idx >= 0)
        {
            // Exact match — this offset is the start of a line
            line = idx + 1;
        }
        else
        {
            // ~idx is the index of the first element greater than offset.Value
            // so the line containing this offset is at index (~idx - 1)
            line = ~idx; // ~idx - 1 + 1
        }

        var column = offset.Value - _lineStartChars[line - 1] + 1;
        return new DocumentPosition(line, column);
    }

    public DocumentOffset PositionToOffset(DocumentPosition position)
    {
        ValidateLine(position.Line);
        var lineStart = _lineStartChars[position.Line - 1];
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

        if (_batchDepth == 0)
        {
            // Piece tree was mutated — mark caches stale before rebuilding line starts
            _bytesDirty = true;
            _textDirty = true;
            _cachedByteMap = null;
            // Rebuild line starts from bytes (ensures _cachedBytes is rebuilt)
            RebuildLineStartsFromPieces();
        }

        var result = new EditResult(previousVersion, _version, applied, inverse);

        if (_batchDepth == 0)
        {
            Changed?.Invoke(this, new DocumentChangedEventArgs(
                _version, previousVersion, applied, inverse, source));
        }
        else
        {
            _batchApplied?.AddRange(applied);
            _batchInverse?.AddRange(inverse);
        }

        return result;
    }

    // ── Byte-level editing ──────────────────────────────────────

    public EditResult ApplyBytes(ByteEditOperation operation, string? source = null)
    {
        var previousVersion = _version;

        // Convert to char-level inverse for undo compatibility
        EnsureTextValid();
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
        // During a batch, _cachedText is stale. All text reads and char→byte
        // conversions use _cachedText, so clamp against its actual length.
        var textLength = _cachedText.Length;

        switch (operation)
        {
            case InsertOperation insert:
            {
                var clampedOffset = Math.Min(insert.Offset.Value, textLength);
                var byteOffset = CharOffsetToByteOffset(clampedOffset);
                var textBytes = Encoding.UTF8.GetBytes(insert.Text);
                InsertBytesInternal(byteOffset, textBytes);
                if (_batchDepth > 0)
                    _batchLengthDelta += insert.Text.Length;
                var deleteInverse = new DeleteOperation(
                    new DocumentRange(new DocumentOffset(clampedOffset), new DocumentOffset(clampedOffset + insert.Text.Length)));
                return (insert, deleteInverse);
            }

            case DeleteOperation delete:
            {
                var clampedEnd = Math.Min(delete.Range.End.Value, textLength);
                var clampedStart = Math.Min(delete.Range.Start.Value, clampedEnd);
                var range = new DocumentRange(new DocumentOffset(clampedStart), new DocumentOffset(clampedEnd));
                if (range.IsEmpty) return (delete, new InsertOperation(range.Start, ""));

                var deletedText = GetText(range);
                var byteStart = CharOffsetToByteOffset(range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(range.End.Value);
                DeleteBytesInternal(byteStart, byteEnd - byteStart);
                if (_batchDepth > 0)
                    _batchLengthDelta -= range.Length;
                var insertInverse = new InsertOperation(range.Start, deletedText);
                return (delete, insertInverse);
            }

            case ReplaceOperation replace:
            {
                var clampedEnd = Math.Min(replace.Range.End.Value, textLength);
                var clampedStart = Math.Min(replace.Range.Start.Value, clampedEnd);
                var range = new DocumentRange(new DocumentOffset(clampedStart), new DocumentOffset(clampedEnd));

                var replacedText = range.IsEmpty ? "" : GetText(range);
                var byteStart = CharOffsetToByteOffset(range.Start.Value);
                var byteEnd = CharOffsetToByteOffset(range.End.Value);
                if (byteEnd > byteStart) DeleteBytesInternal(byteStart, byteEnd - byteStart);
                var textBytes = Encoding.UTF8.GetBytes(replace.NewText);
                InsertBytesInternal(byteStart, textBytes);
                if (_batchDepth > 0)
                    _batchLengthDelta += replace.NewText.Length - range.Length;
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
    /// Converts a character offset to a byte offset.
    /// Uses line starts for O(log L + line_length) instead of O(n).
    /// During batch mode, falls back to cached text scan.
    /// </summary>
    private int CharOffsetToByteOffset(int charOffset)
    {
        if (charOffset <= 0) return 0;

        // During batch, _lineStartChars is stale — fall back to cached text
        if (_batchDepth > 0)
        {
            if (charOffset >= _cachedText.Length) return ByteCount;
            return Encoding.UTF8.GetByteCount(_cachedText.AsSpan(0, charOffset));
        }

        // Quick check: if text isn't dirty and offset is at/past end, return ByteCount
        if (!_textDirty && charOffset >= _cachedText.Length) return ByteCount;

        // Binary search to find which line contains this char offset
        var lineIdx = _lineStartChars.BinarySearch(charOffset);
        int lineIndex;
        if (lineIdx >= 0)
        {
            // Exact match on a line start
            return _lineStartBytes[lineIdx];
        }
        else
        {
            // ~lineIdx is the first line start AFTER charOffset
            lineIndex = ~lineIdx - 1;
        }

        if (lineIndex < 0) lineIndex = 0;

        var lineCharStart = _lineStartChars[lineIndex];
        var lineByteStart = _lineStartBytes[lineIndex];
        var charsIntoLine = charOffset - lineCharStart;

        if (charsIntoLine == 0)
            return lineByteStart;

        // Read the line's bytes from pieces and count UTF-8 chars up to the offset
        int lineByteEnd;
        if (lineIndex + 1 < _lineStartBytes.Count)
            lineByteEnd = _lineStartBytes[lineIndex + 1];
        else
            lineByteEnd = ByteCount;

        var lineByteLength = lineByteEnd - lineByteStart;
        if (lineByteLength <= 0) return lineByteStart;

        var lineBytes = new byte[lineByteLength];
        CopyBytesTo(lineBytes, lineByteStart, lineByteLength);

        // Walk UTF-8 byte sequences counting chars until we reach the target
        var bytePos = 0;
        var charCount = 0;
        while (bytePos < lineByteLength && charCount < charsIntoLine)
        {
            var b = lineBytes[bytePos];
            int seqLen;
            if (b < 0x80) seqLen = 1;
            else if (b < 0xC0) seqLen = 1; // continuation byte (invalid lead) → replacement char
            else if (b < 0xE0) seqLen = 2;
            else if (b < 0xF0) seqLen = 3;
            else seqLen = 4;

            // Validate continuation bytes exist
            if (bytePos + seqLen > lineByteLength)
                seqLen = 1; // truncated sequence → one replacement char

            bytePos += seqLen;
            charCount++;
        }

        return lineByteStart + bytePos;
    }

    // ── Piece tree helpers ────────────────────────────────────────

    /// <summary>
    /// Copies bytes from the piece tree into the destination array.
    /// Uses PieceTree.ReadRange for O(log n) start.
    /// </summary>
    private void CopyBytesTo(byte[] dest, int byteOffset, int count)
    {
        var destPos = 0;
        _pieceTree.ReadRange(byteOffset, count, (source, start, length) =>
        {
            if (source == PieceTree.BufferSource.Original)
            {
                _originalBuffer.Span.Slice(start, length).CopyTo(dest.AsSpan(destPos));
            }
            else
            {
                for (var i = 0; i < length; i++)
                    dest[destPos + i] = _addBuffer[start + i];
            }

            destPos += length;
        });
    }

    /// <summary>
    /// Reads bytes from the piece tree for a byte range and decodes as UTF-8.
    /// O(log n + byteCount) — avoids materializing the full document.
    /// </summary>
    private string ReadTextFromPieces(int byteOffset, int byteCount)
    {
        if (byteCount <= 0) return string.Empty;
        var buffer = new byte[byteCount];
        CopyBytesTo(buffer, byteOffset, byteCount);
        return Encoding.UTF8.GetString(buffer);
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

    // ── Line starts management ───────────────────────────────────

    /// <summary>
    /// Rebuilds line starts by scanning bytes from the piece tree directly.
    /// Avoids materializing _cachedText. O(n) single-pass scan.
    /// Uses Encoding.UTF8.GetCharCount on each line segment for accuracy with
    /// malformed UTF-8 (matches .NET's decoder behavior for replacement chars).
    /// </summary>
    private void RebuildLineStartsFromPieces()
    {
        EnsureBytesValid();

        _lineStartChars = [0];
        _lineStartBytes = [0];

        var lastByteOffset = 0;
        var charPos = 0;
        for (var i = 0; i < _cachedBytes.Length; i++)
        {
            if (_cachedBytes[i] == (byte)'\n')
            {
                // Count chars in [lastByteOffset, i+1) using actual UTF-8 decoder
                charPos += Encoding.UTF8.GetCharCount(_cachedBytes, lastByteOffset, i + 1 - lastByteOffset);
                lastByteOffset = i + 1;
                _lineStartBytes.Add(i + 1);
                _lineStartChars.Add(charPos);
            }
        }
    }

    /// <summary>
    /// Invalidates cached text and bytes so they are lazily rebuilt on next access.
    /// Line starts remain valid (rebuilt from pieces).
    /// </summary>
    /// <summary>Ensures <c>_cachedBytes</c> is up-to-date with the piece tree.</summary>
    private void EnsureBytesValid()
    {
        if (!_bytesDirty) return;
        _cachedBytes = AssembleBytes();
        _bytesDirty = false;
    }

    /// <summary>Ensures <c>_cachedText</c> is up-to-date with the piece tree.</summary>
    private void EnsureTextValid()
    {
        if (!_textDirty) return;
        // Build text directly from pieces — avoids double O(n) via bytes
        if (_bytesDirty)
        {
            _cachedBytes = AssembleBytes();
            _bytesDirty = false;
            _cachedText = Encoding.UTF8.GetString(_cachedBytes);
        }
        else
        {
            _cachedText = Encoding.UTF8.GetString(_cachedBytes);
        }
        _textDirty = false;
    }

    /// <summary>
    /// Rebuilds all cached derived state from bytes: text, line starts.
    /// Used after batch edits and byte-level edits.
    /// </summary>
    private void RebuildCaches()
    {
        _cachedBytes = AssembleBytes();
        _bytesDirty = false;
        _cachedText = Encoding.UTF8.GetString(_cachedBytes);
        _textDirty = false;
        _cachedByteMap = null;
        RebuildLineStarts();
    }

    private void RebuildLineStarts()
    {
        _lineStartChars = [0];
        _lineStartBytes = [0];
        for (var i = 0; i < _cachedBytes.Length; i++)
        {
            if (_cachedBytes[i] == (byte)'\n')
            {
                _lineStartBytes.Add(i + 1);
            }
        }

        for (var i = 0; i < _cachedText.Length; i++)
        {
            if (_cachedText[i] == '\n')
            {
                _lineStartChars.Add(i + 1);
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
