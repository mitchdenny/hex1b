using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// Default IHex1bDocument implementation backed by a piece table.
/// Phase 1 uses a simple piece list; Phase 3 upgrades to a red-black piece tree.
/// </summary>
public sealed class Hex1bDocument : IHex1bDocument
{
    private readonly ReadOnlyMemory<char> _originalBuffer;
    private readonly StringBuilder _addBuffer = new();
    private readonly List<Piece> _pieces = new();
    private List<int> _lineStarts = new();
    private long _version;

    public int Length { get; private set; }
    public int LineCount => _lineStarts.Count;
    public long Version => _version;

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    public Hex1bDocument(string initialText = "")
    {
        _originalBuffer = initialText.AsMemory();
        Length = initialText.Length;

        if (initialText.Length > 0)
        {
            _pieces.Add(new Piece(BufferSource.Original, 0, initialText.Length));
        }

        RebuildLineStarts();
    }

    public string GetText()
    {
        var sb = new StringBuilder(Length);
        foreach (var piece in _pieces)
        {
            AppendPiece(sb, piece);
        }
        return sb.ToString();
    }

    public string GetText(DocumentRange range)
    {
        if (range.IsEmpty) return string.Empty;
        if (range.End.Value > Length)
            throw new ArgumentOutOfRangeException(nameof(range));

        var sb = new StringBuilder(range.Length);
        var remaining = range.Length;
        var offset = 0;

        foreach (var piece in _pieces)
        {
            if (remaining <= 0) break;

            var pieceEnd = offset + piece.Length;
            if (pieceEnd <= range.Start.Value)
            {
                offset = pieceEnd;
                continue;
            }

            var startInPiece = Math.Max(0, range.Start.Value - offset);
            var endInPiece = Math.Min(piece.Length, range.End.Value - offset);
            var count = endInPiece - startInPiece;

            AppendPieceSlice(sb, piece, startInPiece, count);
            remaining -= count;
            offset = pieceEnd;
        }

        return sb.ToString();
    }

    public string GetLineText(int line)
    {
        ValidateLine(line);
        var startOffset = _lineStarts[line - 1];
        int endOffset;

        if (line < LineCount)
        {
            // End at the start of next line, minus the line ending
            endOffset = _lineStarts[line];
            var text = GetText(new DocumentRange(new DocumentOffset(startOffset), new DocumentOffset(endOffset)));
            // Strip trailing \r\n or \n
            if (text.EndsWith("\r\n")) return text[..^2];
            if (text.EndsWith('\n')) return text[..^1];
            return text;
        }

        // Last line: goes to end of document
        endOffset = Length;
        return GetText(new DocumentRange(new DocumentOffset(startOffset), new DocumentOffset(endOffset)));
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

        // Binary search for the line containing this offset
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
            var (appliedOp, inverseOp) = ApplyOne(op);
            applied.Add(appliedOp);
            inverse.Add(inverseOp);
        }

        _version++;
        RebuildLineStarts();

        var result = new EditResult(previousVersion, _version, applied, inverse);
        Changed?.Invoke(this, new DocumentChangedEventArgs(
            _version, previousVersion, applied, inverse, source));

        return result;
    }

    private (EditOperation Applied, EditOperation Inverse) ApplyOne(EditOperation operation)
    {
        switch (operation)
        {
            case InsertOperation insert:
                InsertInternal(insert.Offset.Value, insert.Text);
                var deleteInverse = new DeleteOperation(
                    new DocumentRange(insert.Offset, insert.Offset + insert.Text.Length));
                return (insert, deleteInverse);

            case DeleteOperation delete:
                var deletedText = GetText(delete.Range);
                DeleteInternal(delete.Range.Start.Value, delete.Range.Length);
                var insertInverse = new InsertOperation(delete.Range.Start, deletedText);
                return (delete, insertInverse);

            case ReplaceOperation replace:
                var replacedText = GetText(replace.Range);
                DeleteInternal(replace.Range.Start.Value, replace.Range.Length);
                InsertInternal(replace.Range.Start.Value, replace.NewText);
                var replaceInverse = new ReplaceOperation(
                    new DocumentRange(replace.Range.Start, replace.Range.Start + replace.NewText.Length),
                    replacedText);
                return (replace, replaceInverse);

            default:
                throw new ArgumentException($"Unknown operation type: {operation.GetType()}", nameof(operation));
        }
    }

    private void InsertInternal(int offset, string text)
    {
        if (text.Length == 0) return;

        var addStart = _addBuffer.Length;
        _addBuffer.Append(text);
        var newPiece = new Piece(BufferSource.Added, addStart, text.Length);

        if (_pieces.Count == 0)
        {
            _pieces.Add(newPiece);
            Length += text.Length;
            return;
        }

        // Find the piece containing the offset and split it
        var (pieceIndex, offsetInPiece) = FindPieceAt(offset);

        if (pieceIndex == _pieces.Count)
        {
            // Insert at the very end
            _pieces.Add(newPiece);
        }
        else if (offsetInPiece == 0)
        {
            // Insert before the piece
            _pieces.Insert(pieceIndex, newPiece);
        }
        else if (offsetInPiece == _pieces[pieceIndex].Length)
        {
            // Insert after the piece
            _pieces.Insert(pieceIndex + 1, newPiece);
        }
        else
        {
            // Split the piece
            var existing = _pieces[pieceIndex];
            var left = new Piece(existing.Source, existing.Start, offsetInPiece);
            var right = new Piece(existing.Source, existing.Start + offsetInPiece, existing.Length - offsetInPiece);
            _pieces[pieceIndex] = left;
            _pieces.Insert(pieceIndex + 1, newPiece);
            _pieces.Insert(pieceIndex + 2, right);
        }

        Length += text.Length;
    }

    private void DeleteInternal(int offset, int length)
    {
        if (length == 0) return;

        var end = offset + length;
        var (startPiece, startInPiece) = FindPieceAt(offset);
        var (endPiece, endInPiece) = FindPieceAt(end);

        // Build new piece list for the affected range
        var newPieces = new List<Piece>();

        // Left remainder of start piece
        if (startInPiece > 0)
        {
            var piece = _pieces[startPiece];
            newPieces.Add(new Piece(piece.Source, piece.Start, startInPiece));
        }

        // Right remainder of end piece
        if (endPiece < _pieces.Count && endInPiece < _pieces[endPiece].Length)
        {
            var piece = _pieces[endPiece];
            newPieces.Add(new Piece(piece.Source, piece.Start + endInPiece, piece.Length - endInPiece));
        }

        // Replace the range
        var removeCount = (endPiece < _pieces.Count ? endPiece + 1 : _pieces.Count) - startPiece;
        _pieces.RemoveRange(startPiece, removeCount);
        _pieces.InsertRange(startPiece, newPieces);

        Length -= length;
    }

    private (int PieceIndex, int OffsetInPiece) FindPieceAt(int offset)
    {
        var current = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            if (offset <= current + _pieces[i].Length)
            {
                return (i, offset - current);
            }
            current += _pieces[i].Length;
        }
        return (_pieces.Count, 0);
    }

    private void AppendPiece(StringBuilder sb, Piece piece)
    {
        AppendPieceSlice(sb, piece, 0, piece.Length);
    }

    private void AppendPieceSlice(StringBuilder sb, Piece piece, int start, int count)
    {
        if (piece.Source == BufferSource.Original)
        {
            sb.Append(_originalBuffer.Span.Slice(piece.Start + start, count));
        }
        else
        {
            // StringBuilder doesn't have a Span-based Append for slicing itself,
            // so we read char by char for the add buffer
            for (var i = 0; i < count; i++)
            {
                sb.Append(_addBuffer[piece.Start + start + i]);
            }
        }
    }

    private void RebuildLineStarts()
    {
        _lineStarts = [0]; // Line 1 always starts at offset 0
        var text = GetText();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    private void ValidateLine(int line)
    {
        if (line < 1 || line > LineCount)
            throw new ArgumentOutOfRangeException(nameof(line), $"Line {line} is out of range [1..{LineCount}].");
    }

    private enum BufferSource { Original, Added }
    private readonly record struct Piece(BufferSource Source, int Start, int Length);
}
