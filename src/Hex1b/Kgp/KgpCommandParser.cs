using System.Diagnostics.CodeAnalysis;

namespace Hex1b;

internal static class KgpCommandParser
{
    internal enum ErrorCode
    {
        None,
        EmptyControlPair,
        MalformedControlPair,
        InvalidControlKey,
        MissingValue,
        InvalidDelimiter,
        InvalidAction,
        InvalidDeleteTarget,
        InvalidTransmissionMedium,
        InvalidCompression,
        InvalidMoreData,
        InvalidImageFormat,
        InvalidSignedInteger,
        InvalidUnsignedInteger,
        ConflictingImageIdentity,
    }

    internal readonly record struct Failure(
        ErrorCode Code,
        char Key,
        int Position,
        int ValueStart,
        int ValueLength,
        KgpAction? Action,
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        KgpParsedCommand.QuietMode Quiet)
    {
        internal string FormatReason(ReadOnlySpan<char> controlData)
        {
            var value = GetValue(controlData);
            return Code switch
            {
                ErrorCode.EmptyControlPair
                    => $"Empty control pair at position {Position}.",
                ErrorCode.MalformedControlPair
                    => $"Malformed control pair '{value.ToString()}'.",
                ErrorCode.InvalidControlKey
                    => $"Invalid control key '{Key}'.",
                ErrorCode.MissingValue
                    => $"Missing value for control key '{Key}'.",
                ErrorCode.InvalidDelimiter
                    => $"Invalid delimiter in value for control key '{Key}'.",
                ErrorCode.InvalidAction
                    => $"Invalid action value '{value.ToString()}'.",
                ErrorCode.InvalidDeleteTarget
                    => $"Invalid delete target '{value.ToString()}'.",
                ErrorCode.InvalidTransmissionMedium
                    => $"Invalid transmission medium '{value.ToString()}'.",
                ErrorCode.InvalidCompression
                    => $"Invalid compression value '{value.ToString()}'.",
                ErrorCode.InvalidMoreData
                    => $"Invalid more-data value '{value.ToString()}'.",
                ErrorCode.InvalidImageFormat
                    => $"Invalid image format '{value.ToString()}'.",
                ErrorCode.InvalidSignedInteger
                    => $"Invalid signed integer '{value.ToString()}' for control key '{Key}'.",
                ErrorCode.InvalidUnsignedInteger
                    => $"Invalid unsigned integer '{value.ToString()}' for control key '{Key}'.",
                ErrorCode.ConflictingImageIdentity
                    => "Must not specify both image id and image number",
                _ => "Invalid KGP control data.",
            };
        }

        private ReadOnlySpan<char> GetValue(ReadOnlySpan<char> controlData)
        {
            if (ValueStart < 0 ||
                ValueLength < 0 ||
                ValueStart > controlData.Length - ValueLength)
            {
                return [];
            }

            return controlData.Slice(ValueStart, ValueLength);
        }
    }

    private enum ValidationResult
    {
        Unknown,
        Valid,
        Invalid,
    }

    private readonly record struct ParseError(
        ErrorCode Code,
        char Key,
        int Position,
        int ValueStart,
        int ValueLength);

    private struct ErrorCandidates
    {
        internal ParseError Grammar;
        internal ParseError Action;
        internal ParseError Validation;

        internal readonly ParseError Preferred
            => Grammar.Code != ErrorCode.None
                ? Grammar
                : Action.Code != ErrorCode.None
                    ? Action
                    : Validation;
    }

    private struct ControlSlot
    {
        internal int Position;
        internal int ValueStart;
        internal int ValueLength;

        internal readonly bool IsPresent => ValueLength > 0;
    }

    internal static bool TryParse(
        string? controlData,
        [NotNullWhen(true)] out KgpParsedCommand? command,
        out Failure failure)
        => TryParse(controlData.AsSpan(), out command, out failure);

    internal static bool TryParse(
        ReadOnlySpan<char> controlData,
        [NotNullWhen(true)] out KgpParsedCommand? command,
        out Failure failure)
    {
        Span<ControlSlot> slots = stackalloc ControlSlot[52];
        slots.Clear();

        var errors = default(ErrorCandidates);
        var hasActionControl = false;
        var controlKeys = default(KgpControlKeySet);
        if (!controlData.IsEmpty)
        {
            ScanControlData(
                controlData,
                slots,
                ref errors,
                ref hasActionControl,
                ref controlKeys);
        }

        var action = HasValue(slots, 'a')
            ? ReadAction(controlData, slots)
            : hasActionControl
                ? (KgpAction?)null
                : KgpAction.Transmit;
        var imageId = ReadUInt32(controlData, slots, 'i');
        var imageNumber = ReadUInt32(controlData, slots, 'I');
        var placementId = ReadUInt32(controlData, slots, 'p');
        var quiet = ToQuietMode(ReadUInt32(controlData, slots, 'q'));
        var error = errors.Preferred;

        if (error.Code != ErrorCode.None)
        {
            command = null;
            failure = new Failure(
                error.Code,
                error.Key,
                error.Position,
                error.ValueStart,
                error.ValueLength,
                action,
                imageId,
                imageNumber,
                placementId,
                quiet);
            return false;
        }

        if (imageId > 0 && imageNumber > 0)
        {
            var slot = slots[GetKeyIndex('I')];
            command = null;
            failure = new Failure(
                ErrorCode.ConflictingImageIdentity,
                'I',
                slot.Position,
                slot.ValueStart,
                slot.ValueLength,
                action,
                imageId,
                imageNumber,
                placementId,
                quiet);
            return false;
        }

        command = action!.Value switch
        {
            KgpAction.Transmit => new KgpParsedCommand.Transmit(
                ParseTransmission(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.TransmitAndDisplay => new KgpParsedCommand.TransmitAndDisplay(
                ParseTransmission(controlData, slots),
                ParseDisplay(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.Query => new KgpParsedCommand.Query(
                ParseTransmission(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.Put => new KgpParsedCommand.Put(
                ParseDisplay(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.Delete => new KgpParsedCommand.Delete(
                ParseDelete(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.AnimationFrame => new KgpParsedCommand.AnimationFrame(
                ParseTransmission(controlData, slots),
                ParseAnimationFrame(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.AnimationControl => new KgpParsedCommand.AnimationControl(
                ParseAnimationControl(controlData, slots),
                quiet,
                controlKeys),
            KgpAction.Compose => new KgpParsedCommand.Compose(
                ParseComposition(controlData, slots),
                quiet,
                controlKeys),
            _ => throw new InvalidOperationException(
                $"Unsupported KGP action: {action.Value}."),
        };
        failure = default;
        return true;
    }

    private static void ScanControlData(
        ReadOnlySpan<char> controlData,
        Span<ControlSlot> slots,
        ref ErrorCandidates errors,
        ref bool hasActionControl,
        ref KgpControlKeySet controlKeys)
    {
        var pairStart = 0;
        var pairIndex = 0;

        while (pairStart <= controlData.Length)
        {
            var remaining = controlData[pairStart..];
            var commaIndex = remaining.IndexOf(',');
            var pairLength = commaIndex >= 0
                ? commaIndex
                : remaining.Length;

            ParsePair(
                controlData,
                pairStart,
                pairLength,
                pairIndex,
                slots,
                ref errors,
                ref hasActionControl,
                ref controlKeys);

            if (commaIndex < 0)
                break;

            pairStart += pairLength + 1;
            pairIndex++;
        }
    }

    private static void ParsePair(
        ReadOnlySpan<char> controlData,
        int pairStart,
        int pairLength,
        int pairIndex,
        Span<ControlSlot> slots,
        ref ErrorCandidates errors,
        ref bool hasActionControl,
        ref KgpControlKeySet controlKeys)
    {
        if (pairLength == 0)
        {
            RecordFirstError(
                ref errors.Grammar,
                ErrorCode.EmptyControlPair,
                key: default,
                pairIndex,
                pairStart,
                pairLength);
            return;
        }

        var pair = controlData.Slice(pairStart, pairLength);
        var equalsIndex = pair.IndexOf('=');
        if (equalsIndex != 1 || pair[(equalsIndex + 1)..].IndexOf('=') >= 0)
        {
            RecordFirstError(
                ref errors.Grammar,
                ErrorCode.MalformedControlPair,
                key: default,
                pairIndex,
                pairStart,
                pairLength);
            return;
        }

        var key = pair[0];
        if (!IsAsciiLetter(key))
        {
            RecordFirstError(
                ref errors.Grammar,
                ErrorCode.InvalidControlKey,
                key,
                pairIndex,
                pairStart,
                pairLength);
            return;
        }

        if (key == 'a')
            hasActionControl = true;

        var valueStart = pairStart + 2;
        var valueLength = pairLength - 2;
        if (valueLength == 0)
        {
            RecordFirstError(
                ref errors.Grammar,
                ErrorCode.MissingValue,
                key,
                pairIndex,
                valueStart,
                valueLength);
            return;
        }

        var value = controlData.Slice(valueStart, valueLength);
        if (value.IndexOf(';') >= 0)
        {
            RecordFirstError(
                ref errors.Grammar,
                ErrorCode.InvalidDelimiter,
                key,
                pairIndex,
                valueStart,
                valueLength);
            return;
        }

        controlKeys = controlKeys.Add(key);

        var validation = ValidateKnownValue(key, value, out var errorCode);
        if (validation == ValidationResult.Unknown)
            return;

        if (validation == ValidationResult.Invalid)
        {
            if (key == 'a')
            {
                RecordFirstError(
                    ref errors.Action,
                    errorCode,
                    key,
                    pairIndex,
                    valueStart,
                    valueLength);
            }
            else
            {
                RecordFirstError(
                    ref errors.Validation,
                    errorCode,
                    key,
                    pairIndex,
                    valueStart,
                    valueLength);
            }
            return;
        }

        ref var slot = ref slots[GetKeyIndex(key)];
        slot.Position = pairIndex;
        slot.ValueStart = valueStart;
        slot.ValueLength = valueLength;
    }

    private static void RecordFirstError(
        ref ParseError firstError,
        ErrorCode code,
        char key,
        int position,
        int valueStart,
        int valueLength)
    {
        if (firstError.Code != ErrorCode.None)
            return;

        firstError = new ParseError(
            code,
            key,
            position,
            valueStart,
            valueLength);
    }

    private static ValidationResult ValidateKnownValue(
        char key,
        ReadOnlySpan<char> value,
        out ErrorCode errorCode)
    {
        switch (key)
        {
            case 'a':
                if (!TryParseAction(value, out _))
                {
                    errorCode = ErrorCode.InvalidAction;
                    return ValidationResult.Invalid;
                }
                break;
            case 'd':
                if (!TryParseDeleteTarget(value, out _))
                {
                    errorCode = ErrorCode.InvalidDeleteTarget;
                    return ValidationResult.Invalid;
                }
                break;
            case 't':
                if (!TryParseMedium(value, out _))
                {
                    errorCode = ErrorCode.InvalidTransmissionMedium;
                    return ValidationResult.Invalid;
                }
                break;
            case 'o':
                if (value.Length != 1 || value[0] != 'z')
                {
                    errorCode = ErrorCode.InvalidCompression;
                    return ValidationResult.Invalid;
                }
                break;
            case 'm':
                if (value.Length != 1 || value[0] is not ('0' or '1'))
                {
                    errorCode = ErrorCode.InvalidMoreData;
                    return ValidationResult.Invalid;
                }
                break;
            case 'f':
                if (!TryParseFormat(value, out _))
                {
                    errorCode = ErrorCode.InvalidImageFormat;
                    return ValidationResult.Invalid;
                }
                break;
            case 'z':
            case 'H':
            case 'V':
                if (!TryParseInt32(value, out _))
                {
                    errorCode = ErrorCode.InvalidSignedInteger;
                    return ValidationResult.Invalid;
                }
                break;
            case 'q':
            case 's':
            case 'v':
            case 'S':
            case 'O':
            case 'i':
            case 'I':
            case 'p':
            case 'N':
            case 'x':
            case 'y':
            case 'w':
            case 'h':
            case 'X':
            case 'Y':
            case 'c':
            case 'r':
            case 'C':
            case 'U':
            case 'P':
            case 'Q':
                if (!TryParseUInt32(value, out _))
                {
                    errorCode = ErrorCode.InvalidUnsignedInteger;
                    return ValidationResult.Invalid;
                }
                break;
            default:
                errorCode = ErrorCode.None;
                return ValidationResult.Unknown;
        }

        errorCode = ErrorCode.None;
        return ValidationResult.Valid;
    }

    private static KgpParsedCommand.QuietMode ToQuietMode(uint value)
        => value switch
        {
            0 => KgpParsedCommand.QuietMode.Normal,
            1 => KgpParsedCommand.QuietMode.SuppressSuccess,
            _ => KgpParsedCommand.QuietMode.SuppressAll,
        };

    private static KgpParsedCommand.TransmissionData ParseTransmission(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
        => new(
            ReadFormat(controlData, slots),
            ReadMedium(controlData, slots),
            ReadUInt32(controlData, slots, 's'),
            ReadUInt32(controlData, slots, 'v'),
            ReadUInt32(controlData, slots, 'S'),
            ReadUInt32(controlData, slots, 'O'),
            ReadUInt32(controlData, slots, 'i'),
            ReadUInt32(controlData, slots, 'I'),
            ReadUInt32(controlData, slots, 'p'),
            HasValue(slots, 'o')
                ? KgpParsedCommand.CompressionMode.Zlib
                : KgpParsedCommand.CompressionMode.None,
            ReadUInt32(controlData, slots, 'm') != 0,
            ReadUInt32(controlData, slots, 'N'));

    private static KgpParsedCommand.DisplayData ParseDisplay(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
        => new(
            ReadUInt32(controlData, slots, 'i'),
            ReadUInt32(controlData, slots, 'I'),
            ReadUInt32(controlData, slots, 'p'),
            ReadUInt32(controlData, slots, 'x'),
            ReadUInt32(controlData, slots, 'y'),
            ReadUInt32(controlData, slots, 'w'),
            ReadUInt32(controlData, slots, 'h'),
            ReadUInt32(controlData, slots, 'X'),
            ReadUInt32(controlData, slots, 'Y'),
            ReadUInt32(controlData, slots, 'c'),
            ReadUInt32(controlData, slots, 'r'),
            ReadUInt32(controlData, slots, 'C') == 1,
            ReadUInt32(controlData, slots, 'U') != 0,
            ReadInt32(controlData, slots, 'z'),
            ReadUInt32(controlData, slots, 'P'),
            ReadUInt32(controlData, slots, 'Q'),
            ReadInt32(controlData, slots, 'H'),
            ReadInt32(controlData, slots, 'V'));

    private static KgpParsedCommand.DeleteSelector ParseDelete(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
    {
        var target = HasValue(slots, 'd')
            ? ReadValue(controlData, slots, 'd')[0]
            : 'a';

        return target switch
        {
            'a' => new KgpParsedCommand.DeleteSelector.All(false),
            'A' => new KgpParsedCommand.DeleteSelector.All(true),
            'i' => new KgpParsedCommand.DeleteSelector.ById(
                false,
                ReadUInt32(controlData, slots, 'i'),
                ReadUInt32(controlData, slots, 'p')),
            'I' => new KgpParsedCommand.DeleteSelector.ById(
                true,
                ReadUInt32(controlData, slots, 'i'),
                ReadUInt32(controlData, slots, 'p')),
            'n' => new KgpParsedCommand.DeleteSelector.ByNumber(
                false,
                ReadUInt32(controlData, slots, 'I'),
                ReadUInt32(controlData, slots, 'p')),
            'N' => new KgpParsedCommand.DeleteSelector.ByNumber(
                true,
                ReadUInt32(controlData, slots, 'I'),
                ReadUInt32(controlData, slots, 'p')),
            'c' => new KgpParsedCommand.DeleteSelector.AtCursor(false),
            'C' => new KgpParsedCommand.DeleteSelector.AtCursor(true),
            'f' => new KgpParsedCommand.DeleteSelector.AnimationFrames(
                false,
                ReadUInt32(controlData, slots, 'i'),
                ReadUInt32(controlData, slots, 'I'),
                ReadUInt32(controlData, slots, 'r')),
            'F' => new KgpParsedCommand.DeleteSelector.AnimationFrames(
                true,
                ReadUInt32(controlData, slots, 'i'),
                ReadUInt32(controlData, slots, 'I'),
                ReadUInt32(controlData, slots, 'r')),
            'p' => new KgpParsedCommand.DeleteSelector.AtCell(
                false,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y')),
            'P' => new KgpParsedCommand.DeleteSelector.AtCell(
                true,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y')),
            'q' => new KgpParsedCommand.DeleteSelector.AtCellWithZIndex(
                false,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y'),
                ReadInt32(controlData, slots, 'z')),
            'Q' => new KgpParsedCommand.DeleteSelector.AtCellWithZIndex(
                true,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y'),
                ReadInt32(controlData, slots, 'z')),
            'r' => new KgpParsedCommand.DeleteSelector.ByRange(
                false,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y')),
            'R' => new KgpParsedCommand.DeleteSelector.ByRange(
                true,
                ReadUInt32(controlData, slots, 'x'),
                ReadUInt32(controlData, slots, 'y')),
            'x' => new KgpParsedCommand.DeleteSelector.ByColumn(
                false,
                ReadUInt32(controlData, slots, 'x')),
            'X' => new KgpParsedCommand.DeleteSelector.ByColumn(
                true,
                ReadUInt32(controlData, slots, 'x')),
            'y' => new KgpParsedCommand.DeleteSelector.ByRow(
                false,
                ReadUInt32(controlData, slots, 'y')),
            'Y' => new KgpParsedCommand.DeleteSelector.ByRow(
                true,
                ReadUInt32(controlData, slots, 'y')),
            'z' => new KgpParsedCommand.DeleteSelector.ByZIndex(
                false,
                ReadInt32(controlData, slots, 'z')),
            'Z' => new KgpParsedCommand.DeleteSelector.ByZIndex(
                true,
                ReadInt32(controlData, slots, 'z')),
            _ => throw new InvalidOperationException(
                $"Unsupported KGP delete target: {target}."),
        };
    }

    private static KgpParsedCommand.AnimationFrameData ParseAnimationFrame(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
        => new(
            ReadUInt32(controlData, slots, 'x'),
            ReadUInt32(controlData, slots, 'y'),
            ReadUInt32(controlData, slots, 'c'),
            ReadUInt32(controlData, slots, 'r'),
            ReadInt32(controlData, slots, 'z'),
            ReadUInt32(controlData, slots, 'X') == 1
                ? KgpParsedCommand.CompositionMode.Overwrite
                : KgpParsedCommand.CompositionMode.AlphaBlend,
            ReadUInt32(controlData, slots, 'Y'));

    private static KgpParsedCommand.AnimationControlData ParseAnimationControl(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
        => new(
            ReadUInt32(controlData, slots, 'i'),
            ReadUInt32(controlData, slots, 'I'),
            ReadUInt32(controlData, slots, 'p'),
            ReadUInt32(controlData, slots, 's') switch
            {
                1 => KgpParsedCommand.AnimationPlaybackState.Stopped,
                2 => KgpParsedCommand.AnimationPlaybackState.Loading,
                3 => KgpParsedCommand.AnimationPlaybackState.Running,
                _ => KgpParsedCommand.AnimationPlaybackState.None,
            },
            ReadUInt32(controlData, slots, 'v'),
            ReadUInt32(controlData, slots, 'c'),
            ReadUInt32(controlData, slots, 'r'),
            ReadInt32(controlData, slots, 'z'));

    private static KgpParsedCommand.CompositionData ParseComposition(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
        => new(
            ReadUInt32(controlData, slots, 'i'),
            ReadUInt32(controlData, slots, 'I'),
            ReadUInt32(controlData, slots, 'p'),
            ReadUInt32(controlData, slots, 'c'),
            ReadUInt32(controlData, slots, 'r'),
            ReadUInt32(controlData, slots, 'x'),
            ReadUInt32(controlData, slots, 'y'),
            ReadUInt32(controlData, slots, 'w'),
            ReadUInt32(controlData, slots, 'h'),
            ReadUInt32(controlData, slots, 'X'),
            ReadUInt32(controlData, slots, 'Y'),
            ReadUInt32(controlData, slots, 'C') == 0
                ? KgpParsedCommand.CompositionMode.AlphaBlend
                : KgpParsedCommand.CompositionMode.Overwrite);

    private static KgpAction ReadAction(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
    {
        if (!HasValue(slots, 'a'))
            return KgpAction.Transmit;

        _ = TryParseAction(
            ReadValue(controlData, slots, 'a'),
            out var action);
        return action;
    }

    private static KgpFormat ReadFormat(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
    {
        if (!HasValue(slots, 'f'))
            return KgpFormat.Rgba32;

        _ = TryParseFormat(
            ReadValue(controlData, slots, 'f'),
            out var format);
        return format;
    }

    private static KgpTransmissionMedium ReadMedium(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots)
    {
        if (!HasValue(slots, 't'))
            return KgpTransmissionMedium.Direct;

        _ = TryParseMedium(
            ReadValue(controlData, slots, 't'),
            out var medium);
        return medium;
    }

    private static uint ReadUInt32(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots,
        char key)
    {
        if (!HasValue(slots, key))
            return 0;

        _ = TryParseUInt32(
            ReadValue(controlData, slots, key),
            out var result);
        return result;
    }

    private static int ReadInt32(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots,
        char key)
    {
        if (!HasValue(slots, key))
            return 0;

        _ = TryParseInt32(
            ReadValue(controlData, slots, key),
            out var result);
        return result;
    }

    private static bool HasValue(
        ReadOnlySpan<ControlSlot> slots,
        char key)
        => slots[GetKeyIndex(key)].IsPresent;

    private static ReadOnlySpan<char> ReadValue(
        ReadOnlySpan<char> controlData,
        ReadOnlySpan<ControlSlot> slots,
        char key)
    {
        ref readonly var slot = ref slots[GetKeyIndex(key)];
        return slot.IsPresent
            ? controlData.Slice(slot.ValueStart, slot.ValueLength)
            : [];
    }

    private static bool TryParseAction(
        ReadOnlySpan<char> value,
        out KgpAction action)
    {
        if (value.Length != 1)
        {
            action = default;
            return false;
        }

        action = value[0] switch
        {
            't' => KgpAction.Transmit,
            'T' => KgpAction.TransmitAndDisplay,
            'q' => KgpAction.Query,
            'p' => KgpAction.Put,
            'd' => KgpAction.Delete,
            'f' => KgpAction.AnimationFrame,
            'a' => KgpAction.AnimationControl,
            'c' => KgpAction.Compose,
            _ => default,
        };
        return value[0] is 't' or 'T' or 'q' or 'p' or 'd' or 'f' or 'a' or 'c';
    }

    private static bool TryParseFormat(
        ReadOnlySpan<char> value,
        out KgpFormat format)
    {
        if (value.SequenceEqual("24"))
        {
            format = KgpFormat.Rgb24;
            return true;
        }

        if (value.SequenceEqual("32"))
        {
            format = KgpFormat.Rgba32;
            return true;
        }

        if (value.SequenceEqual("100"))
        {
            format = KgpFormat.Png;
            return true;
        }

        format = default;
        return false;
    }

    private static bool TryParseMedium(
        ReadOnlySpan<char> value,
        out KgpTransmissionMedium medium)
    {
        if (value.Length != 1)
        {
            medium = default;
            return false;
        }

        medium = value[0] switch
        {
            'd' => KgpTransmissionMedium.Direct,
            'f' => KgpTransmissionMedium.File,
            't' => KgpTransmissionMedium.TempFile,
            's' => KgpTransmissionMedium.SharedMemory,
            _ => default,
        };
        return value[0] is 'd' or 'f' or 't' or 's';
    }

    private static bool TryParseDeleteTarget(
        ReadOnlySpan<char> value,
        out KgpDeleteTarget target)
    {
        if (value.Length != 1)
        {
            target = default;
            return false;
        }

        target = value[0] switch
        {
            'a' => KgpDeleteTarget.All,
            'A' => KgpDeleteTarget.AllFreeData,
            'i' => KgpDeleteTarget.ById,
            'I' => KgpDeleteTarget.ByIdFreeData,
            'n' => KgpDeleteTarget.ByNumber,
            'N' => KgpDeleteTarget.ByNumberFreeData,
            'c' => KgpDeleteTarget.AtCursor,
            'C' => KgpDeleteTarget.AtCursorFreeData,
            'p' => KgpDeleteTarget.AtCell,
            'P' => KgpDeleteTarget.AtCellFreeData,
            'q' => KgpDeleteTarget.AtCellWithZIndex,
            'Q' => KgpDeleteTarget.AtCellWithZIndexFreeData,
            'x' => KgpDeleteTarget.ByColumn,
            'X' => KgpDeleteTarget.ByColumnFreeData,
            'y' => KgpDeleteTarget.ByRow,
            'Y' => KgpDeleteTarget.ByRowFreeData,
            'z' => KgpDeleteTarget.ByZIndex,
            'Z' => KgpDeleteTarget.ByZIndexFreeData,
            'r' => KgpDeleteTarget.ByRange,
            'R' => KgpDeleteTarget.ByRangeFreeData,
            'f' => KgpDeleteTarget.AnimationFrames,
            'F' => KgpDeleteTarget.AnimationFramesFreeData,
            _ => default,
        };
        return value[0] is
            'a' or 'A' or 'i' or 'I' or 'n' or 'N' or 'c' or 'C' or
            'p' or 'P' or 'q' or 'Q' or 'x' or 'X' or 'y' or 'Y' or
            'z' or 'Z' or 'r' or 'R' or 'f' or 'F';
    }

    private static bool TryParseUInt32(
        ReadOnlySpan<char> value,
        out uint result)
    {
        if (value.IsEmpty)
        {
            result = 0;
            return false;
        }

        uint parsed = 0;
        foreach (var character in value)
        {
            if (!IsAsciiDigit(character))
            {
                result = 0;
                return false;
            }

            var digit = (uint)(character - '0');
            if (parsed > (uint.MaxValue - digit) / 10)
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digit;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseInt32(
        ReadOnlySpan<char> value,
        out int result)
    {
        if (value.IsEmpty)
        {
            result = 0;
            return false;
        }

        var negative = value[0] == '-';
        var digits = negative ? value[1..] : value;
        if (digits.IsEmpty)
        {
            result = 0;
            return false;
        }

        var limit = negative
            ? (uint)int.MaxValue + 1
            : int.MaxValue;
        uint parsed = 0;
        foreach (var character in digits)
        {
            if (!IsAsciiDigit(character))
            {
                result = 0;
                return false;
            }

            var digit = (uint)(character - '0');
            if (parsed > (limit - digit) / 10)
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digit;
        }

        if (!negative)
        {
            result = (int)parsed;
            return true;
        }

        result = parsed == (uint)int.MaxValue + 1
            ? int.MinValue
            : -(int)parsed;
        return true;
    }

    private static int GetKeyIndex(char key)
        => key is >= 'a' and <= 'z'
            ? key - 'a'
            : 26 + key - 'A';

    private static bool IsAsciiLetter(char value)
        => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiDigit(char value)
        => value is >= '0' and <= '9';
}
