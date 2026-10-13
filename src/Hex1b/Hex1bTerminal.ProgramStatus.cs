using System.Text;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private TerminalProgramStatusSnapshot _programStatus = new([]);
    private TerminalProgramStatusSnapshot? _programStatusRestoreBaseline;
    private bool _discardingProgramStatus;
    private bool _discardedProgramStatusEscape;

    /// <summary>Gets the current immutable inventory of OSC 7501 program reports.</summary>
    /// <remarks>
    /// Reports belong to the terminal, not the active screen. Working and blocked reports
    /// expire at the next shell prompt or local process exit. Done and error reports survive.
    /// RIS clears every report; soft reset does not. OSC 9 progress is not mapped to this inventory.
    /// </remarks>
    public TerminalProgramStatusSnapshot ProgramStatus
    {
        get { lock (_bufferLock) return _programStatus; }
    }

    /// <summary>Occurs when the immutable program inventory changes.</summary>
    /// <remarks>Subscribing does not emit the baseline. Read <see cref="ProgramStatus"/> for current reports.</remarks>
    public event Action<TerminalProgramStatusSnapshot>? ProgramStatusChanged;

    internal void RestoreProgramStatus(TerminalProgramStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        bool invalidate;
        lock (_bufferLock)
        {
            SetProgramStatus(snapshot);
            invalidate = _activityRestoreDepth == 0;
        }
        if (invalidate)
            PresentationInvalidated?.Invoke();
    }

    private void SetProgramStatus(TerminalProgramStatusSnapshot snapshot)
    {
        if (_programStatus.ContentEquals(snapshot)) return;
        _programStatus = snapshot;
        if (_activityRestoreDepth == 0)
            ProgramStatusChanged?.Invoke(snapshot);
    }

    private void ExpireProgramStatus()
        => SetProgramStatus(new(_programStatus.Records.Where(record =>
            record.State is not (TerminalProgramStatusState.Working or TerminalProgramStatusState.Blocked))));

    private async Task ExpireLocalProgramStatusAsync(CancellationToken ct)
    {
        var outputLock = _hmp1OutputStateLock;
        if (outputLock is not null)
            await outputLock.WaitAsync(ct);
        try
        {
            lock (_bufferLock)
                ExpireProgramStatus();
        }
        finally
        {
            outputLock?.Release();
        }
    }

    private void ProcessProgramStatus(string body, int sequenceBytes)
    {
        if (body == "?")
        {
            _ = SendProtocolResponseAsync(Encoding.ASCII.GetBytes(
                "\x1b]7501;?:states=idle,working,done,blocked,error:kinds=permission,question,auth\x1b\\"));
            return;
        }
        if (!TerminalProgramStatusCodec.TryParse(body, sequenceBytes, out var report, out var clearId))
            return;
        var records = _programStatus.Records.ToList();
        if (clearId is not null)
        {
            records.RemoveAll(record => record.Id == clearId ||
                TerminalProgramStatusSnapshot.IsDescendant(record.Id, clearId));
        }
        else
        {
            records.RemoveAll(record => record.Id == report!.Id);
            if (records.Count == 256)
                records.RemoveAt(0);
            records.Add(report!);
        }
        SetProgramStatus(new(records));
    }

    // Only OSC 7501 has this retention policy; other escape families keep their existing framing.
    private string FilterProgramStatusFraming(string text, bool flushAtBoundary)
    {
        StringBuilder? output = null;
        var retainedStart = 0;
        var i = 0;
        if (_discardingProgramStatus)
        {
            output = new StringBuilder();
            if (_discardedProgramStatusEscape)
            {
                _discardedProgramStatusEscape = false;
                if (text.Length == 0) return "";
                if (text[0] == '\\')
                {
                    _discardingProgramStatus = false;
                    i = 1;
                }
                else
                {
                    _discardingProgramStatus = false;
                    text = "\x1b" + text;
                }
            }
            while (_discardingProgramStatus && i < text.Length)
            {
                var c = text[i++];
                if (c is '\x07' or '\x9c' or '\x18' or '\x1a')
                    _discardingProgramStatus = false;
                else if (c == '\x1b')
                {
                    if (i == text.Length)
                        _discardedProgramStatusEscape = true;
                    else if (text[i] == '\\')
                    {
                        i++;
                        _discardingProgramStatus = false;
                    }
                    else
                    {
                        i--;
                        _discardingProgramStatus = false;
                    }
                    break;
                }
            }
            retainedStart = i;
        }

        while (i < text.Length)
        {
            var prefixLength = text.AsSpan(i).StartsWith("\x1b]7501;", StringComparison.Ordinal) ? 7 :
                text.AsSpan(i).StartsWith("\u009d7501;", StringComparison.Ordinal) ? 6 : 0;
            if (prefixLength == 0)
            {
                if (IsEscapeSequenceIntroducer(text[i], false))
                {
                    if (!TryFindEscapeSequenceEnd(text, i, out var end)) break;
                    i = end;
                }
                else i++;
                continue;
            }
            var start = i;
            i += prefixLength;
            var bytes = 7; // C1 introducer is two bytes in UTF-8.
            var discard = false;
            var complete = false;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '\x1b')
                {
                    if (i + 1 == text.Length)
                    {
                        if (++bytes > TerminalProgramStatusCodec.MaximumSequenceBytes)
                        {
                            discard = true;
                            _discardingProgramStatus = true;
                        }
                        break;
                    }
                    if (text[i + 1] == '\\')
                    {
                        bytes += 2;
                        i += 2;
                        complete = true;
                    }
                    else discard = true; // Abort and reprocess the new escape introducer.
                    break;
                }
                i++;
                bytes += c <= 0x7f ? 1 : c <= 0x7ff ? 2 : 3;
                if (c is '\x07' or '\x9c')
                {
                    complete = true;
                    break;
                }
                if (c is '\x18' or '\x1a')
                {
                    discard = true;
                    break;
                }
                if (bytes > TerminalProgramStatusCodec.MaximumSequenceBytes)
                {
                    discard = true;
                    _discardingProgramStatus = true;
                    break;
                }
            }
            discard |= bytes > TerminalProgramStatusCodec.MaximumSequenceBytes || flushAtBoundary && !complete;
            if (discard)
            {
                output ??= new StringBuilder(text.Length);
                output.Append(text.AsSpan(retainedStart, start - retainedStart));
                retainedStart = i;
                if (_discardingProgramStatus)
                {
                    // Finish consuming this chunk without retaining the oversized body.
                    var tail = text[i..];
                    var filteredTail = FilterProgramStatusFraming(tail, flushAtBoundary);
                    output.Append(filteredTail);
                    return output.ToString();
                }
            }
            if (!complete && !discard) break;
        }
        if (output is null) return text;
        output.Append(text.AsSpan(retainedStart));
        return output.ToString();
    }
}
