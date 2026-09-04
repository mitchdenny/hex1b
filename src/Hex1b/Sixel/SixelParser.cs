using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// The authoritative outcome of parsing a Sixel DCS sequence.
/// </summary>
/// <remarks>
/// Automation code can use this to assert whether a graphic parsed cleanly or
/// degraded, without inspecting raw diagnostics. See
/// <see cref="Hex1b.SixelData.Outcome"/> and <see cref="Hex1b.SixelData.Diagnostics"/>
/// for the explanatory detail behind a non-<see cref="Complete"/> outcome.
/// </remarks>
public enum SixelParseOutcome
{
    /// <summary>The sequence parsed to completion with no downgrades.</summary>
    Complete,

    /// <summary>The upstream stream cancelled the sequence before it terminated.</summary>
    Cancelled,

    /// <summary>The sequence was structurally invalid.</summary>
    Malformed,

    /// <summary>The sequence parsed, but bounded retention limits downgraded it.</summary>
    LimitDowngraded,

    /// <summary>The DCS introducer was not an accepted Sixel form.</summary>
    Rejected,
}

/// <summary>
/// Whether unpainted Sixel pixels resolve to the captured background color or
/// remain transparent.
/// </summary>
public enum SixelBackgroundMode
{
    /// <summary>Unpainted pixels resolve to the captured background color.</summary>
    Opaque,

    /// <summary>Unpainted pixels remain transparent.</summary>
    Transparent,
}

internal enum SixelColorSpace
{
    Hls = 1,
    Rgb = 2,
}

/// <summary>
/// Explicit reasons a Sixel parse degraded or was annotated, surfaced via
/// <see cref="SixelDiagnostic"/> on <see cref="Hex1b.SixelData.Diagnostics"/>.
/// </summary>
public enum SixelDiagnosticCode
{
    /// <summary>The DCS introducer is not an accepted Sixel form.</summary>
    RejectedIntroducer,

    /// <summary>The header carried more parameters than the policy allows.</summary>
    ExcessiveHeaderParameters,

    /// <summary>The pixel-aspect-ratio macro is not one this parser supports.</summary>
    UnsupportedAspectMacro,

    /// <summary>A byte outside the accepted Sixel alphabet was encountered.</summary>
    InvalidByte,

    /// <summary>A command ended before it was fully specified.</summary>
    IncompleteCommand,

    /// <summary>A later command replaced an earlier, conflicting one.</summary>
    ReplacedCommand,

    /// <summary>A command carried more parameters than the policy allows.</summary>
    ExcessiveCommandParameters,

    /// <summary>A numeric parameter exceeded the implementation's coordinate limit.</summary>
    NumericLimitExceeded,

    /// <summary>Geometry accumulation saturated at the coordinate limit.</summary>
    GeometrySaturated,

    /// <summary>The raster attributes (DECGRA) command was invalid.</summary>
    InvalidRasterAttributes,

    /// <summary>A palette definition or selection command was invalid.</summary>
    InvalidPaletteCommand,

    /// <summary>A bounded metadata limit (e.g. palette entries) was exceeded.</summary>
    MetadataLimitExceeded,

    /// <summary>Bounded command retention truncated the remaining sequence.</summary>
    CommandRetentionLimitExceeded,

    /// <summary>The DCS sequence ended before a string terminator.</summary>
    UnterminatedSequence,
}

internal enum SixelCommandKind
{
    Data,
    Palette,
}

internal readonly record struct SixelPoint(int X, int Y);

/// <summary>
/// A width/height pixel extent.
/// </summary>
/// <param name="Width">The width, in pixels.</param>
/// <param name="Height">The height, in pixels.</param>
public readonly record struct SixelExtent(int Width, int Height)
{
    /// <summary>An empty (zero-size) extent.</summary>
    public static SixelExtent Empty { get; } = new(0, 0);
}

/// <summary>
/// A pixel-space rectangle.
/// </summary>
/// <param name="X">The left offset, in pixels.</param>
/// <param name="Y">The top offset, in pixels.</param>
/// <param name="Width">The width, in pixels.</param>
/// <param name="Height">The height, in pixels.</param>
public readonly record struct SixelBounds(int X, int Y, int Width, int Height)
{
    /// <summary>An empty (zero-size) bounds.</summary>
    public static SixelBounds Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Gets whether this bounds has zero width or height.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;
}

/// <summary>
/// A pixel aspect ratio expressed as a numerator/denominator pair (DECGRA/DECSIXEL macro).
/// </summary>
/// <param name="Numerator">The vertical scale numerator.</param>
/// <param name="Denominator">The vertical scale denominator.</param>
public readonly record struct SixelAspectRatio(int Numerator, int Denominator);

internal readonly record struct SixelHeader(
    int PixelAspectMacro,
    int BackgroundSelection,
    int HorizontalGridSize,
    SixelAspectRatio AspectRatio,
    SixelBackgroundMode BackgroundMode);

internal readonly record struct SixelRasterAttributes(
    int Pan,
    int Pad,
    int Ph,
    int Pv);

internal readonly record struct SixelPaletteCommand(
    int Register,
    SixelColorSpace? ColorSpace,
    int? X,
    int? Y,
    int? Z)
{
    public bool IsDefinition => ColorSpace is not null;
}

internal readonly record struct SixelCommand(
    SixelCommandKind Kind,
    int X,
    int Band,
    int Value,
    int RepeatCount,
    SixelPaletteCommand? Palette);

/// <summary>
/// A single explicit Sixel parse diagnostic explaining a degraded or annotated outcome.
/// </summary>
/// <param name="Code">The specific reason this diagnostic was raised.</param>
/// <param name="Offset">The byte offset into the payload where the condition was observed.</param>
/// <param name="Command">The offending command byte, when applicable.</param>
/// <param name="Message">A human-readable explanation.</param>
public readonly record struct SixelDiagnostic(
    SixelDiagnosticCode Code,
    long Offset,
    byte? Command,
    string Message);

internal sealed record SixelParseResult(
    SixelHeader Header,
    SixelRasterAttributes? RasterAttributes,
    SixelPoint GraphicsCursor,
    SixelPoint MaximumCommandOrDataPosition,
    SixelExtent DeclaredExtent,
    SixelExtent DataExtent,
    SixelBounds PaintedBounds,
    SixelExtent LogicalCanvasExtent,
    int SelectedColorRegister,
    IReadOnlyList<SixelPaletteCommand> PaletteMutations,
    IReadOnlyList<SixelCommand> Commands,
    bool CommandsComplete,
    SixelParseOutcome Outcome,
    IReadOnlyList<SixelDiagnostic> Diagnostics)
{
    public static SixelParseResult Rejected(
        DcsIntroducer introducer,
        DcsSequenceStatus status,
        bool retentionLimitExceeded)
    {
        var outcome = status switch
        {
            DcsSequenceStatus.Cancelled => SixelParseOutcome.Cancelled,
            DcsSequenceStatus.Malformed or DcsSequenceStatus.Unterminated => SixelParseOutcome.Malformed,
            _ when retentionLimitExceeded => SixelParseOutcome.LimitDowngraded,
            _ => SixelParseOutcome.Rejected,
        };
        var code = status == DcsSequenceStatus.Unterminated
            ? SixelDiagnosticCode.UnterminatedSequence
            : SixelDiagnosticCode.RejectedIntroducer;
        var message = status == DcsSequenceStatus.Unterminated
            ? "The DCS sequence ended before a string terminator."
            : "The DCS introducer is not an accepted Sixel form.";

        return new SixelParseResult(
            CreateHeader(introducer.Parameters),
            null,
            new SixelPoint(0, 0),
            new SixelPoint(0, 0),
            SixelExtent.Empty,
            SixelExtent.Empty,
            SixelBounds.Empty,
            SixelExtent.Empty,
            0,
            Array.Empty<SixelPaletteCommand>(),
            Array.Empty<SixelCommand>(),
            false,
            outcome,
            [new SixelDiagnostic(code, 0, introducer.FinalByte, message)]);
    }

    internal static SixelHeader CreateHeader(IReadOnlyList<int?> parameters)
    {
        var p1 = GetParameter(parameters, 0);
        var p2 = GetParameter(parameters, 1);
        var p3 = GetParameter(parameters, 2);
        return new SixelHeader(
            p1,
            p2,
            p3,
            SixelParser.GetAspectRatio(p1),
            p2 == 1 ? SixelBackgroundMode.Transparent : SixelBackgroundMode.Opaque);
    }

    private static int GetParameter(IReadOnlyList<int?> parameters, int index) =>
        index < parameters.Count ? parameters[index] ?? 0 : 0;
}

internal sealed class SixelParser
{
    internal const int MaximumNumericValue = DcsByteStreamParser.DefaultMaximumParameterValue;
    internal const int MaximumRetainedCommandCount = 65_536;
    internal const int MaximumPaletteMutationCount = 4_096;
    internal const int MaximumDiagnosticCount = 64;

    private readonly List<SixelCommand> _commands = [];
    private readonly List<SixelPaletteCommand> _paletteMutations = [];
    private readonly List<SixelDiagnostic> _diagnostics = [];
    private readonly int?[] _parameters = new int?[5];
    private readonly bool[] _parameterOverflowReported = new bool[5];
    private readonly SixelHeader _header;
    private SixelAspectRatio _aspectRatio;
    private SixelRasterAttributes? _rasterAttributes;
    private PendingCommand _pendingCommand;
    private int _parameterCount;
    private int _graphicsX;
    private int _graphicsY;
    private int _band;
    private int _maximumX;
    private int _maximumY;
    private int _dataWidth;
    private int _dataHeight;
    private int _selectedColorRegister;
    private int _paintedMinX = int.MaxValue;
    private int _paintedMinY = int.MaxValue;
    private int _paintedMaxX;
    private int _paintedMaxY;
    private long _offset;
    private bool _malformed;
    private bool _limitDowngraded;
    private bool _retainCommands = true;
    private bool _commandsComplete = true;
    private bool _metadataLimitReported;
    private bool _commandLimitReported;

    public SixelParser(DcsIntroducer introducer)
    {
        _header = SixelParseResult.CreateHeader(introducer.Parameters);
        _aspectRatio = _header.AspectRatio;

        if (introducer.Parameters.Count > 3)
        {
            MarkMalformed(
                SixelDiagnosticCode.ExcessiveHeaderParameters,
                null,
                "Sixel accepts at most three DCS header parameters.");
        }

        if (_header.PixelAspectMacro is < 0 or > 9)
        {
            AddDiagnostic(
                SixelDiagnosticCode.UnsupportedAspectMacro,
                null,
                "The unsupported P1 aspect macro uses the DEC 2:1 default.");
        }
    }

    public static SixelParseResult ParsePayload(string payload)
    {
        var bytes = EncodePayload(payload);
        if (bytes.AsSpan().StartsWith("\x1bP"u8) || (bytes.Length > 0 && bytes[0] == 0x90))
        {
            var parser = new DcsByteStreamParser();
            var batch = parser.Process(bytes);
            if (batch.Frames.Count > 0)
            {
                return batch.Frames[0].Frame.SixelResult;
            }

            var final = parser.Complete();
            if (final.Frames.Count > 0)
            {
                return final.Frames[0].Frame.SixelResult;
            }

            return SixelParseResult.Rejected(
                new DcsIntroducer(
                    null,
                    Array.Empty<int?>(),
                    Array.Empty<byte>(),
                    null,
                    false),
                DcsSequenceStatus.Unterminated,
                retentionLimitExceeded: false);
        }

        var frame = DcsByteStreamParser.ParseCompleteContent(bytes);
        if (frame.Introducer.IsSixel)
        {
            return frame.SixelResult;
        }

        var bodyParser = new SixelParser(new DcsIntroducer(
            null,
            Array.Empty<int?>(),
            Array.Empty<byte>(),
            (byte)'q',
            true));
        foreach (var value in bytes)
        {
            bodyParser.ProcessByte(value, retainCommand: true);
        }
        return bodyParser.Complete(DcsSequenceStatus.Complete, retentionLimitExceeded: false);
    }

    internal static byte[] EncodePayload(string payload) =>
        payload.Any(value => value > byte.MaxValue)
            ? System.Text.Encoding.UTF8.GetBytes(payload)
            : System.Text.Encoding.Latin1.GetBytes(payload);

    public void ProcessByte(byte value, bool retainCommand)
    {
        _retainCommands &= retainCommand;
        var reprocess = true;
        while (reprocess)
        {
            reprocess = false;
            if (_pendingCommand == PendingCommand.Repeat)
            {
                if (value is >= (byte)'0' and <= (byte)'9')
                {
                    AccumulateDigit(value);
                    break;
                }

                if (value is >= (byte)'?' and <= (byte)'~')
                {
                    var repeatCount = _parameters[0] ?? 1;
                    if (repeatCount == 0)
                    {
                        repeatCount = 1;
                    }
                    ApplyData(value - (byte)'?', repeatCount, retainCommand);
                    ResetPendingCommand();
                    break;
                }

                MarkMalformed(
                    IsCommandIntroducer(value)
                        ? SixelDiagnosticCode.ReplacedCommand
                        : SixelDiagnosticCode.IncompleteCommand,
                    (byte)'!',
                    "DECGRI was not followed by a Sixel data byte.");
                ResetPendingCommand();
                reprocess = true;
                continue;
            }

            if (_pendingCommand is PendingCommand.RasterAttributes or PendingCommand.Palette)
            {
                if (value is >= (byte)'0' and <= (byte)'9')
                {
                    AccumulateDigit(value);
                    break;
                }

                if (value == (byte)';')
                {
                    AdvanceParameter();
                    break;
                }

                CompleteParameterCommand(retainCommand);
                reprocess = true;
                continue;
            }

            switch (value)
            {
                case >= (byte)'?' and <= (byte)'~':
                    ApplyData(value - (byte)'?', 1, retainCommand);
                    break;
                case (byte)'!':
                    BeginCommand(PendingCommand.Repeat);
                    break;
                case (byte)'$':
                    _graphicsX = 0;
                    ObserveCursor();
                    break;
                case (byte)'-':
                    _graphicsX = 0;
                    _graphicsY = SaturatingAdd(_graphicsY, ScaleBandHeight(), (byte)'-');
                    _band = SaturatingAdd(_band, 1, (byte)'-');
                    ObserveCursor();
                    break;
                case (byte)'"':
                    BeginCommand(PendingCommand.RasterAttributes);
                    break;
                case (byte)'#':
                    BeginCommand(PendingCommand.Palette);
                    break;
                default:
                    MarkMalformed(
                        SixelDiagnosticCode.InvalidByte,
                        value,
                        "The payload contains a byte that is not valid in Sixel data.");
                    break;
            }
        }

        _offset++;
    }

    public SixelParseResult Complete(
        DcsSequenceStatus status,
        bool retentionLimitExceeded)
    {
        if (_pendingCommand != PendingCommand.None)
        {
            if (_pendingCommand is PendingCommand.RasterAttributes or PendingCommand.Palette)
            {
                CompleteParameterCommand(_retainCommands);
            }
            else
            {
                MarkMalformed(
                    SixelDiagnosticCode.IncompleteCommand,
                    PendingCommandByte(_pendingCommand),
                    "The Sixel sequence ended with an incomplete command.");
                ResetPendingCommand();
            }
        }

        if (status == DcsSequenceStatus.Unterminated)
        {
            MarkMalformed(
                SixelDiagnosticCode.UnterminatedSequence,
                null,
                "The DCS sequence ended before a string terminator.");
        }

        if (retentionLimitExceeded)
        {
            _limitDowngraded = true;
            _commandsComplete = false;
        }

        var declared = _rasterAttributes is { } raster
            ? new SixelExtent(raster.Ph, raster.Pv)
            : SixelExtent.Empty;
        var painted = _paintedMinX == int.MaxValue
            ? SixelBounds.Empty
            : new SixelBounds(
                _paintedMinX,
                _paintedMinY,
                _paintedMaxX - _paintedMinX,
                _paintedMaxY - _paintedMinY);
        var logical = new SixelExtent(
            Math.Max(declared.Width, Math.Max(_dataWidth, _paintedMaxX)),
            Math.Max(declared.Height, Math.Max(_dataHeight, _paintedMaxY)));
        var outcome = status switch
        {
            DcsSequenceStatus.Cancelled => SixelParseOutcome.Cancelled,
            DcsSequenceStatus.Malformed or DcsSequenceStatus.Unterminated => SixelParseOutcome.Malformed,
            _ when _malformed => SixelParseOutcome.Malformed,
            _ when _limitDowngraded => SixelParseOutcome.LimitDowngraded,
            _ => SixelParseOutcome.Complete,
        };

        return new SixelParseResult(
            _header with { AspectRatio = _aspectRatio },
            _rasterAttributes,
            new SixelPoint(_graphicsX, _graphicsY),
            new SixelPoint(_maximumX, _maximumY),
            declared,
            new SixelExtent(_dataWidth, _dataHeight),
            painted,
            logical,
            _selectedColorRegister,
            _paletteMutations.ToArray(),
            _commands.ToArray(),
            _commandsComplete,
            outcome,
            _diagnostics.ToArray());
    }

    internal static SixelAspectRatio GetAspectRatio(int pixelAspectMacro) =>
        pixelAspectMacro switch
        {
            2 => new SixelAspectRatio(5, 1),
            3 or 4 => new SixelAspectRatio(3, 1),
            7 or 8 or 9 => new SixelAspectRatio(1, 1),
            _ => new SixelAspectRatio(2, 1),
        };

    private void ApplyData(int mask, int repeatCount, bool retainCommand)
    {
        var startX = _graphicsX;
        var endX = SaturatingAdd(startX, repeatCount, null);
        if (retainCommand)
        {
            AddDataCommand(startX, mask, endX - startX);
        }
        else
        {
            _commandsComplete = false;
        }

        _graphicsX = endX;
        _dataWidth = Math.Max(_dataWidth, endX);
        _dataHeight = Math.Max(
            _dataHeight,
            SaturatingAdd(_graphicsY, ScaleBandHeight(), null));

        if (mask != 0 && endX > startX)
        {
            var firstBit = 0;
            while ((mask & (1 << firstBit)) == 0)
            {
                firstBit++;
            }

            var lastBit = 5;
            while ((mask & (1 << lastBit)) == 0)
            {
                lastBit--;
            }

            var top = SaturatingAdd(_graphicsY, ScaleRowOffsetFloor(firstBit), null);
            var bottom = SaturatingAdd(_graphicsY, ScaleRowOffsetCeiling(lastBit + 1), null);
            _paintedMinX = Math.Min(_paintedMinX, startX);
            _paintedMinY = Math.Min(_paintedMinY, top);
            _paintedMaxX = Math.Max(_paintedMaxX, endX);
            _paintedMaxY = Math.Max(_paintedMaxY, bottom);
        }

        ObserveCursor();
    }

    private void AddDataCommand(int x, int mask, int repeatCount)
    {
        if (repeatCount <= 0)
        {
            return;
        }

        if (_commands.Count > 0)
        {
            var last = _commands[^1];
            if (last.Kind == SixelCommandKind.Data &&
                last.Band == _band &&
                last.Value == mask &&
                last.X + last.RepeatCount == x &&
                last.RepeatCount <= int.MaxValue - repeatCount)
            {
                _commands[^1] = last with { RepeatCount = last.RepeatCount + repeatCount };
                return;
            }
        }

        if (_commands.Count == MaximumRetainedCommandCount)
        {
            _commandsComplete = false;
            _limitDowngraded = true;
            if (!_commandLimitReported)
            {
                _commandLimitReported = true;
                AddDiagnostic(
                    SixelDiagnosticCode.CommandRetentionLimitExceeded,
                    null,
                    "Parsed command retention was disabled after reaching its bounded limit.");
            }
            return;
        }

        _commands.Add(new SixelCommand(
            SixelCommandKind.Data,
            x,
            _band,
            mask,
            repeatCount,
            null));
    }

    private void CompleteParameterCommand(bool retainCommand)
    {
        switch (_pendingCommand)
        {
            case PendingCommand.RasterAttributes:
                ApplyRasterAttributes();
                break;
            case PendingCommand.Palette:
                ApplyPaletteCommand(retainCommand);
                break;
        }

        ResetPendingCommand();
    }

    private void ApplyRasterAttributes()
    {
        if (_parameterCount > 4)
        {
            MarkMalformed(
                SixelDiagnosticCode.InvalidRasterAttributes,
                (byte)'"',
                "DECGRA accepts at most Pan, Pad, Ph, and Pv parameters.");
            return;
        }

        var pan = _parameters[0] ?? 0;
        var pad = _parameters[1] ?? 0;
        var ph = _parameters[2] ?? 0;
        var pv = _parameters[3] ?? 0;
        _rasterAttributes = new SixelRasterAttributes(pan, pad, ph, pv);
        if (pan > 0 && pad > 0)
        {
            _aspectRatio = new SixelAspectRatio(pan, pad);
        }
        else if (pan != 0 || pad != 0)
        {
            MarkMalformed(
                SixelDiagnosticCode.InvalidRasterAttributes,
                (byte)'"',
                "DECGRA Pan and Pad must both be positive to replace the current aspect ratio.");
        }
    }

    private void ApplyPaletteCommand(bool retainCommand)
    {
        if (_parameters[0] is not { } register)
        {
            MarkMalformed(
                SixelDiagnosticCode.InvalidPaletteCommand,
                (byte)'#',
                "DECGCI requires a color register number.");
            return;
        }

        SixelPaletteCommand palette;
        if (_parameterCount == 1)
        {
            palette = new SixelPaletteCommand(register, null, null, null, null);
        }
        else if (_parameterCount == 5 &&
                 _parameters[1] is 1 or 2 &&
                 _parameters[2] is { } x &&
                 _parameters[3] is { } y &&
                 _parameters[4] is { } z)
        {
            palette = new SixelPaletteCommand(
                register,
                (SixelColorSpace)_parameters[1]!.Value,
                x,
                y,
                z);
        }
        else
        {
            _selectedColorRegister = register;
            MarkMalformed(
                SixelDiagnosticCode.InvalidPaletteCommand,
                (byte)'#',
                "DECGCI must select Pc or define Pc;Pu;Px;Py;Pz.");
            return;
        }

        _selectedColorRegister = register;
        if (_paletteMutations.Count < MaximumPaletteMutationCount)
        {
            _paletteMutations.Add(palette);
        }
        else
        {
            _limitDowngraded = true;
            if (!_metadataLimitReported)
            {
                _metadataLimitReported = true;
                AddDiagnostic(
                    SixelDiagnosticCode.MetadataLimitExceeded,
                    (byte)'#',
                    "Palette mutation metadata was truncated at its bounded limit.");
            }
        }

        if (!retainCommand)
        {
            _commandsComplete = false;
            return;
        }

        if (_commands.Count == MaximumRetainedCommandCount)
        {
            _commandsComplete = false;
            _limitDowngraded = true;
            if (!_commandLimitReported)
            {
                _commandLimitReported = true;
                AddDiagnostic(
                    SixelDiagnosticCode.CommandRetentionLimitExceeded,
                    (byte)'#',
                    "Parsed command retention was disabled after reaching its bounded limit.");
            }
            return;
        }

        _commands.Add(new SixelCommand(
            SixelCommandKind.Palette,
            _graphicsX,
            _band,
            0,
            0,
            palette));
    }

    private void BeginCommand(PendingCommand command)
    {
        _pendingCommand = command;
        _parameterCount = 1;
        Array.Clear(_parameters);
        Array.Clear(_parameterOverflowReported);
    }

    private void ResetPendingCommand()
    {
        _pendingCommand = PendingCommand.None;
        _parameterCount = 0;
        Array.Clear(_parameters);
        Array.Clear(_parameterOverflowReported);
    }

    private void AccumulateDigit(byte value)
    {
        var index = _parameterCount - 1;
        var digit = value - (byte)'0';
        var current = _parameters[index] ?? 0;
        if (current > (MaximumNumericValue - digit) / 10)
        {
            _parameters[index] = MaximumNumericValue;
            _limitDowngraded = true;
            if (!_parameterOverflowReported[index])
            {
                _parameterOverflowReported[index] = true;
                AddDiagnostic(
                    SixelDiagnosticCode.NumericLimitExceeded,
                    PendingCommandByte(_pendingCommand),
                    "The numeric parameter was saturated at the implementation limit.");
            }
            return;
        }

        _parameters[index] = (current * 10) + digit;
    }

    private void AdvanceParameter()
    {
        if (_parameterCount == _parameters.Length)
        {
            _malformed = true;
            AddDiagnostic(
                SixelDiagnosticCode.ExcessiveCommandParameters,
                PendingCommandByte(_pendingCommand),
                "The Sixel command contains too many parameters.");
            return;
        }

        _parameterCount++;
    }

    private int ScaleBandHeight() => ScaleRowOffsetCeiling(6);

    private int ScaleRowOffsetFloor(int row)
    {
        var scaled = (long)row * _aspectRatio.Numerator;
        return SaturateScaledValue(scaled / _aspectRatio.Denominator);
    }

    private int ScaleRowOffsetCeiling(int row)
    {
        var scaled = ((long)row * _aspectRatio.Numerator) + _aspectRatio.Denominator - 1;
        return SaturateScaledValue(scaled / _aspectRatio.Denominator);
    }

    private int SaturateScaledValue(long value)
    {
        if (value <= int.MaxValue)
        {
            return (int)value;
        }

        _limitDowngraded = true;
        AddDiagnostic(
            SixelDiagnosticCode.GeometrySaturated,
            (byte)'"',
            "Sixel aspect-scaled geometry was saturated at the implementation coordinate limit.");
        return int.MaxValue;
    }

    private int SaturatingAdd(int left, int right, byte? command)
    {
        if (right <= int.MaxValue - left)
        {
            return left + right;
        }

        _limitDowngraded = true;
        AddDiagnostic(
            SixelDiagnosticCode.GeometrySaturated,
            command,
            "Sixel geometry was saturated at the implementation coordinate limit.");
        return int.MaxValue;
    }

    private void ObserveCursor()
    {
        _maximumX = Math.Max(_maximumX, _graphicsX);
        _maximumY = Math.Max(_maximumY, _graphicsY);
    }

    private void MarkMalformed(
        SixelDiagnosticCode code,
        byte? command,
        string message)
    {
        _malformed = true;
        AddDiagnostic(code, command, message);
    }

    private void AddDiagnostic(
        SixelDiagnosticCode code,
        byte? command,
        string message)
    {
        if (_diagnostics.Count < MaximumDiagnosticCount)
        {
            _diagnostics.Add(new SixelDiagnostic(code, _offset, command, message));
            return;
        }

    }

    private static bool IsCommandIntroducer(byte value) =>
        value is (byte)'!' or (byte)'"' or (byte)'#' or (byte)'$' or (byte)'-';

    private static byte? PendingCommandByte(PendingCommand command) =>
        command switch
        {
            PendingCommand.Repeat => (byte)'!',
            PendingCommand.RasterAttributes => (byte)'"',
            PendingCommand.Palette => (byte)'#',
            _ => null,
        };

    private enum PendingCommand
    {
        None,
        Repeat,
        RasterAttributes,
        Palette,
    }
}
