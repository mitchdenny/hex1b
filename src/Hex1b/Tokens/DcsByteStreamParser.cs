using System.Buffers;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal enum DcsSequenceStatus
{
    Complete,
    Cancelled,
    Malformed,
    Unterminated,
}

internal readonly record struct DcsIntroducer(
    byte? PrivateMarker,
    IReadOnlyList<int?> Parameters,
    IReadOnlyList<byte> Intermediates,
    byte? FinalByte,
    bool IsValid)
{
    public bool IsSixel =>
        IsValid &&
        PrivateMarker is null &&
        Intermediates.Count == 0 &&
        FinalByte == (byte)'q';
}

internal sealed record DcsFrame(
    DcsSequenceStatus Status,
    DcsIntroducer Introducer,
    ReadOnlyMemory<byte> RetainedContent,
    long ByteCount,
    bool RetentionLimitExceeded,
    SixelParseResult SixelResult);

internal readonly record struct DcsFrameBoundary(int TextByteOffset, DcsFrame Frame);

internal sealed record DcsByteStreamBatch(
    ReadOnlyMemory<byte> TextBytes,
    IReadOnlyList<DcsFrameBoundary> Frames)
{
    public static DcsByteStreamBatch Empty { get; } = new(
        ReadOnlyMemory<byte>.Empty,
        Array.Empty<DcsFrameBoundary>());
}

internal sealed class DcsByteStreamParser
{
    internal const int DefaultRetentionLimit = 1024 * 1024;
    internal const int DefaultMaximumParameterCount = 16;
    internal const int DefaultMaximumParameterValue = 999_999_999;
    internal const int MaximumIntermediateCount = 4;

    private readonly int _retentionLimit;
    private readonly int _maximumParameterCount;
    private readonly int _maximumParameterValue;
    private int _utf8ContinuationBytesRemaining;
    private ParserState _state;
    private ParserState _stateBeforeDcsEscape;
    private byte[] _retained = [];
    private int _retainedCount;
    private long _byteCount;
    private bool _retentionLimitExceeded;
    private bool _introducerValid;
    private byte? _privateMarker;
    private int?[] _parameters;
    private int _parameterCount;
    private bool _sawParameterSyntax;
    private bool _inIntermediates;
    private byte[] _intermediates = new byte[MaximumIntermediateCount];
    private int _intermediateCount;
    private byte? _finalByte;
    private SixelParser? _sixelParser;

    public DcsByteStreamParser(
        int retentionLimit = DefaultRetentionLimit,
        int maximumParameterCount = DefaultMaximumParameterCount,
        int maximumParameterValue = DefaultMaximumParameterValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retentionLimit);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumParameterCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumParameterValue, 1);

        _retentionLimit = retentionLimit;
        _maximumParameterCount = maximumParameterCount;
        _maximumParameterValue = maximumParameterValue;
        _parameters = new int?[maximumParameterCount];
    }

    public bool IsInDcs => _state is
        ParserState.Introducer or
        ParserState.Payload or
        ParserState.MalformedIntroducer or
        ParserState.DcsEscape;

    public bool HasPendingInput =>
        _state != ParserState.Ground ||
        _utf8ContinuationBytesRemaining != 0;

    public int RetentionLimit => _retentionLimit;

    public DcsByteStreamBatch Process(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return DcsByteStreamBatch.Empty;
        }

        var text = new ArrayBufferWriter<byte>(Math.Min(data.Length, 256));
        List<DcsFrameBoundary>? frames = null;

        for (var index = 0; index < data.Length; index++)
        {
            var value = data[index];
            var reprocess = true;
            while (reprocess)
            {
                reprocess = false;
                switch (_state)
                {
                    case ParserState.Ground:
                        if (_utf8ContinuationBytesRemaining > 0 &&
                            value is >= 0x80 and <= 0xbf)
                        {
                            WriteByte(text, value);
                            _utf8ContinuationBytesRemaining--;
                        }
                        else if (value == 0x1b)
                        {
                            _utf8ContinuationBytesRemaining = 0;
                            _state = ParserState.GroundEscape;
                        }
                        else if (value == 0x90)
                        {
                            _utf8ContinuationBytesRemaining = 0;
                            StartDcs();
                        }
                        else
                        {
                            _utf8ContinuationBytesRemaining = GetUtf8ContinuationByteCount(value);
                            WriteByte(text, value);
                        }
                        break;

                    case ParserState.GroundEscape:
                        if (value == (byte)'P')
                        {
                            StartDcs();
                        }
                        else
                        {
                            WriteByte(text, 0x1b);
                            _state = ParserState.Ground;
                            reprocess = true;
                        }
                        break;

                    case ParserState.DcsEscape:
                        if (value == (byte)'\\')
                        {
                            AddFrame(
                                text.WrittenCount,
                                CompleteFrame(DcsSequenceStatus.Complete),
                                ref frames);
                        }
                        else
                        {
                            AppendContent(0x1b);
                            ProcessSixelPayloadByte(0x1b);
                            _state = _stateBeforeDcsEscape;
                            reprocess = true;
                        }
                        break;

                    case ParserState.Introducer:
                    case ParserState.Payload:
                    case ParserState.MalformedIntroducer:
                        if (value is 0x18 or 0x1a)
                        {
                            AddFrame(
                                text.WrittenCount,
                                CompleteFrame(DcsSequenceStatus.Cancelled),
                                ref frames);
                        }
                        else if (value == 0x9c)
                        {
                            AddFrame(
                                text.WrittenCount,
                                CompleteFrame(DcsSequenceStatus.Complete),
                                ref frames);
                        }
                        else if (value == 0x1b)
                        {
                            _stateBeforeDcsEscape = _state;
                            _state = ParserState.DcsEscape;
                        }
                        else
                        {
                            AppendContent(value);
                            if (_state == ParserState.Introducer)
                            {
                                ProcessIntroducerByte(value);
                            }
                            else if (_state == ParserState.Payload)
                            {
                                ProcessSixelPayloadByte(value);
                            }
                        }
                        break;
                }
            }
        }

        return new DcsByteStreamBatch(
            text.WrittenMemory.ToArray(),
            frames ?? (IReadOnlyList<DcsFrameBoundary>)Array.Empty<DcsFrameBoundary>());
    }

    public DcsByteStreamBatch Complete()
    {
        if (_state == ParserState.Ground)
        {
            return DcsByteStreamBatch.Empty;
        }

        if (_state == ParserState.GroundEscape)
        {
            _state = ParserState.Ground;
            return new DcsByteStreamBatch(
                new byte[] { 0x1b },
                Array.Empty<DcsFrameBoundary>());
        }

        if (_state == ParserState.DcsEscape)
        {
            AppendContent(0x1b);
            _state = _stateBeforeDcsEscape;
        }

        var frame = CompleteFrame(DcsSequenceStatus.Unterminated);
        return new DcsByteStreamBatch(
            ReadOnlyMemory<byte>.Empty,
            new[] { new DcsFrameBoundary(0, frame) });
    }

    internal static DcsIntroducer ParseIntroducer(
        ReadOnlySpan<byte> content,
        int maximumParameterCount = DefaultMaximumParameterCount,
        int maximumParameterValue = DefaultMaximumParameterValue)
    {
        var parser = new DcsByteStreamParser(
            retentionLimit: 0,
            maximumParameterCount,
            maximumParameterValue);
        parser.StartDcs();

        foreach (var value in content)
        {
            if (parser._state != ParserState.Introducer)
            {
                break;
            }

            if (value <= 0x1f)
            {
                continue;
            }

            parser.ProcessIntroducerByte(value);
        }

        return parser.CreateIntroducer();
    }

    internal static DcsFrame ParseCompleteContent(
        ReadOnlySpan<byte> content,
        int retentionLimit = DefaultRetentionLimit,
        int maximumParameterCount = DefaultMaximumParameterCount,
        int maximumParameterValue = DefaultMaximumParameterValue)
    {
        var parser = new DcsByteStreamParser(
            retentionLimit,
            maximumParameterCount,
            maximumParameterValue);
        _ = parser.Process("\x1bP"u8);
        _ = parser.Process(content);
        var completed = parser.Process("\x1b\\"u8);
        if (completed.Frames.Count > 0)
        {
            return completed.Frames[0].Frame;
        }

        var final = parser.Complete();
        if (final.Frames.Count > 0)
        {
            return final.Frames[0].Frame;
        }

        throw new InvalidOperationException("The synthetic DCS frame did not produce a result.");
    }

    internal static DcsIntroducer ParseIntroducer(
        ReadOnlySpan<char> content,
        int maximumParameterCount = DefaultMaximumParameterCount,
        int maximumParameterValue = DefaultMaximumParameterValue)
    {
        var parser = new DcsByteStreamParser(
            retentionLimit: 0,
            maximumParameterCount,
            maximumParameterValue);
        parser.StartDcs();

        foreach (var value in content)
        {
            if (parser._state != ParserState.Introducer)
            {
                break;
            }

            if (value > byte.MaxValue)
            {
                parser.MarkIntroducerMalformed();
                break;
            }

            parser.ProcessIntroducerByte((byte)value);
        }

        return parser.CreateIntroducer();
    }

    private void StartDcs()
    {
        _state = ParserState.Introducer;
        _stateBeforeDcsEscape = ParserState.Introducer;
        _retained = [];
        _retainedCount = 0;
        _byteCount = 0;
        _retentionLimitExceeded = false;
        _introducerValid = true;
        _privateMarker = null;
        Array.Clear(_parameters);
        _parameterCount = 1;
        _sawParameterSyntax = false;
        _inIntermediates = false;
        _intermediateCount = 0;
        _finalByte = null;
        _sixelParser = null;
    }

    private void ProcessIntroducerByte(byte value)
    {
        if (value <= 0x1f)
        {
            return;
        }

        if (value is >= (byte)'0' and <= (byte)'9')
        {
            if (_inIntermediates)
            {
                MarkIntroducerMalformed();
                return;
            }

            _sawParameterSyntax = true;
            var digit = value - (byte)'0';
            var current = _parameters[_parameterCount - 1] ?? 0;
            if (current > (_maximumParameterValue - digit) / 10)
            {
                MarkIntroducerMalformed();
                return;
            }

            _parameters[_parameterCount - 1] = (current * 10) + digit;
            return;
        }

        if (value == (byte)';')
        {
            if (_inIntermediates || _parameterCount == _maximumParameterCount)
            {
                MarkIntroducerMalformed();
                return;
            }

            _sawParameterSyntax = true;
            _parameterCount++;
            return;
        }

        if (value is >= 0x3c and <= 0x3f)
        {
            if (_privateMarker is not null || _sawParameterSyntax || _inIntermediates)
            {
                MarkIntroducerMalformed();
                return;
            }

            _privateMarker = value;
            return;
        }

        if (value == (byte)':')
        {
            MarkIntroducerMalformed();
            return;
        }

        if (value is >= 0x20 and <= 0x2f)
        {
            _inIntermediates = true;
            if (_intermediateCount == MaximumIntermediateCount)
            {
                MarkIntroducerMalformed();
                return;
            }

            _intermediates[_intermediateCount++] = value;
            return;
        }

        if (value is >= 0x40 and <= 0x7e)
        {
            _finalByte = value;
            _state = _introducerValid
                ? ParserState.Payload
                : ParserState.MalformedIntroducer;
            var introducer = CreateIntroducer();
            if (introducer.IsSixel)
            {
                _sixelParser = new SixelParser(introducer);
            }
            return;
        }

        MarkIntroducerMalformed();
    }

    private void MarkIntroducerMalformed()
    {
        _introducerValid = false;
        _state = ParserState.MalformedIntroducer;
    }

    private void AppendContent(byte value)
    {
        _byteCount++;
        if (_retainedCount == _retentionLimit)
        {
            _retentionLimitExceeded = true;
            return;
        }

        EnsureRetainedCapacity(_retainedCount + 1);
        _retained[_retainedCount++] = value;
    }

    private void EnsureRetainedCapacity(int required)
    {
        if (_retained.Length >= required)
        {
            return;
        }

        var doubled = _retained.Length == 0 ? 64 : _retained.Length * 2;
        var capacity = Math.Min(_retentionLimit, Math.Max(required, doubled));
        Array.Resize(ref _retained, capacity);
    }

    private DcsFrame CompleteFrame(DcsSequenceStatus requestedStatus)
    {
        var status = requestedStatus;
        if (status == DcsSequenceStatus.Complete &&
            (!_introducerValid || _finalByte is null))
        {
            status = DcsSequenceStatus.Malformed;
        }

        var introducer = CreateIntroducer();
        var sixelResult = _sixelParser?.Complete(status, _retentionLimitExceeded)
            ?? SixelParseResult.Rejected(introducer, status, _retentionLimitExceeded);
        var frame = new DcsFrame(
            status,
            introducer,
            _retained.AsMemory(0, _retainedCount),
            _byteCount,
            _retentionLimitExceeded,
            sixelResult);

        _retained = [];
        _retainedCount = 0;
        _state = ParserState.Ground;
        return frame;
    }

    private void ProcessSixelPayloadByte(byte value)
    {
        _sixelParser?.ProcessByte(value, !_retentionLimitExceeded);
    }

    private DcsIntroducer CreateIntroducer()
    {
        IReadOnlyList<int?> parameters = _sawParameterSyntax
            ? _parameters[.._parameterCount]
            : Array.Empty<int?>();
        IReadOnlyList<byte> intermediates = _intermediateCount == 0
            ? Array.Empty<byte>()
            : _intermediates[.._intermediateCount];

        return new DcsIntroducer(
            _privateMarker,
            parameters,
            intermediates,
            _finalByte,
            _introducerValid && _finalByte is not null);
    }

    private static void AddFrame(
        int textByteOffset,
        DcsFrame frame,
        ref List<DcsFrameBoundary>? frames)
    {
        frames ??= [];
        frames.Add(new DcsFrameBoundary(textByteOffset, frame));
    }

    private static void WriteByte(ArrayBufferWriter<byte> writer, byte value)
    {
        writer.GetSpan(1)[0] = value;
        writer.Advance(1);
    }

    private static int GetUtf8ContinuationByteCount(byte value) => value switch
    {
        >= 0xc2 and <= 0xdf => 1,
        >= 0xe0 and <= 0xef => 2,
        >= 0xf0 and <= 0xf4 => 3,
        _ => 0,
    };

    private enum ParserState
    {
        Ground,
        GroundEscape,
        Introducer,
        Payload,
        MalformedIntroducer,
        DcsEscape,
    }
}
