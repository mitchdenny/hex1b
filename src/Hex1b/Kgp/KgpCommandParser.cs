using System.Globalization;

namespace Hex1b;

internal static class KgpCommandParser
{
    internal sealed record Failure(
        string Reason,
        KgpAction? Action,
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        KgpParsedCommand.QuietMode Quiet);

    private readonly record struct ControlPair(char Key, string Value);

    internal static bool TryParse(
        string? controlData,
        out KgpParsedCommand? command,
        out Failure? failure)
    {
        var pairs = new List<ControlPair>();
        var grammarError = ParsePairs(controlData, pairs);
        var context = RecoverContext(pairs);
        var recoveredAction = RecoverAction(pairs);

        if (grammarError is not null)
        {
            command = null;
            failure = CreateFailure(grammarError, recoveredAction, context);
            return false;
        }

        if (!TryResolveAction(pairs, out var action, out var actionError))
        {
            command = null;
            failure = CreateFailure(actionError!, null, context);
            return false;
        }

        var values = new Dictionary<char, string>();
        foreach (var pair in pairs)
        {
            if (!TryValidateKnownValue(pair, out var validationError))
            {
                command = null;
                failure = CreateFailure(validationError!, action, context);
                return false;
            }

            values[pair.Key] = pair.Value;
        }

        var quiet = ParseQuiet(values);
        command = action switch
        {
            KgpAction.Transmit => new KgpParsedCommand.Transmit(
                ParseTransmission(values),
                quiet),
            KgpAction.TransmitAndDisplay => new KgpParsedCommand.TransmitAndDisplay(
                ParseTransmission(values),
                ParseDisplay(values),
                quiet),
            KgpAction.Query => new KgpParsedCommand.Query(
                ParseTransmission(values),
                quiet),
            KgpAction.Put => new KgpParsedCommand.Put(
                ParseDisplay(values),
                quiet),
            KgpAction.Delete => new KgpParsedCommand.Delete(
                ParseDelete(values),
                quiet),
            KgpAction.AnimationFrame => new KgpParsedCommand.AnimationFrame(
                ParseTransmission(values),
                ParseAnimationFrame(values),
                quiet),
            KgpAction.AnimationControl => new KgpParsedCommand.AnimationControl(
                ParseAnimationControl(values),
                quiet),
            KgpAction.Compose => new KgpParsedCommand.Compose(
                ParseComposition(values),
                quiet),
            _ => throw new InvalidOperationException($"Unsupported KGP action: {action}."),
        };
        failure = null;
        return true;
    }

    private static string? ParsePairs(string? controlData, List<ControlPair> pairs)
    {
        if (string.IsNullOrEmpty(controlData))
            return null;

        string? firstError = null;
        var parts = controlData.Split(',');
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0)
            {
                firstError ??= $"Empty control pair at position {index}.";
                continue;
            }

            var equalsIndex = part.IndexOf('=');
            if (equalsIndex != 1 || part.LastIndexOf('=') != equalsIndex)
            {
                firstError ??= $"Malformed control pair '{part}'.";
                continue;
            }

            var key = part[0];
            if (!IsAsciiLetter(key))
            {
                firstError ??= $"Invalid control key '{key}'.";
                continue;
            }

            var value = part[2..];
            if (value.Length == 0)
            {
                firstError ??= $"Missing value for control key '{key}'.";
                continue;
            }

            if (value.Contains(';', StringComparison.Ordinal))
            {
                firstError ??= $"Invalid delimiter in value for control key '{key}'.";
                continue;
            }

            pairs.Add(new ControlPair(key, value));
        }

        return firstError;
    }

    private static bool TryResolveAction(
        List<ControlPair> pairs,
        out KgpAction action,
        out string? error)
    {
        action = KgpAction.Transmit;
        foreach (var pair in pairs)
        {
            if (pair.Key != 'a')
                continue;

            if (!TryParseAction(pair.Value, out action))
            {
                error = $"Invalid action value '{pair.Value}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static KgpAction? RecoverAction(List<ControlPair> pairs)
    {
        var action = KgpAction.Transmit;
        foreach (var pair in pairs)
        {
            if (pair.Key != 'a')
                continue;

            if (!TryParseAction(pair.Value, out action))
                return null;
        }

        return action;
    }

    private static (
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        KgpParsedCommand.QuietMode Quiet) RecoverContext(List<ControlPair> pairs)
    {
        uint imageId = 0;
        uint imageNumber = 0;
        uint placementId = 0;
        var quiet = KgpParsedCommand.QuietMode.Normal;

        foreach (var pair in pairs)
        {
            if (!TryParseUInt32(pair.Value, out var value))
                continue;

            switch (pair.Key)
            {
                case 'i':
                    imageId = value;
                    break;
                case 'I':
                    imageNumber = value;
                    break;
                case 'p':
                    placementId = value;
                    break;
                case 'q':
                    quiet = ToQuietMode(value);
                    break;
            }
        }

        return (imageId, imageNumber, placementId, quiet);
    }

    private static Failure CreateFailure(
        string reason,
        KgpAction? action,
        (
            uint ImageId,
            uint ImageNumber,
            uint PlacementId,
            KgpParsedCommand.QuietMode Quiet) context)
        => new(
            reason,
            action,
            context.ImageId,
            context.ImageNumber,
            context.PlacementId,
            context.Quiet);

    private static bool TryValidateKnownValue(
        ControlPair pair,
        out string? error)
    {
        switch (pair.Key)
        {
            case 'a':
                if (!TryParseAction(pair.Value, out _))
                {
                    error = $"Invalid action value '{pair.Value}'.";
                    return false;
                }
                break;
            case 'd':
                if (!TryParseDeleteTarget(pair.Value, out _))
                {
                    error = $"Invalid delete target '{pair.Value}'.";
                    return false;
                }
                break;
            case 't':
                if (!TryParseMedium(pair.Value, out _))
                {
                    error = $"Invalid transmission medium '{pair.Value}'.";
                    return false;
                }
                break;
            case 'o':
                if (!string.Equals(pair.Value, "z", StringComparison.Ordinal))
                {
                    error = $"Invalid compression value '{pair.Value}'.";
                    return false;
                }
                break;
            case 'f':
                if (!TryParseFormat(pair.Value, out _))
                {
                    error = $"Invalid image format '{pair.Value}'.";
                    return false;
                }
                break;
            case 'z':
            case 'H':
            case 'V':
                if (!TryParseInt32(pair.Value, out _))
                {
                    error = $"Invalid signed integer '{pair.Value}' for control key '{pair.Key}'.";
                    return false;
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
            case 'm':
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
                if (!TryParseUInt32(pair.Value, out _))
                {
                    error = $"Invalid unsigned integer '{pair.Value}' for control key '{pair.Key}'.";
                    return false;
                }
                break;
        }

        error = null;
        return true;
    }

    private static KgpParsedCommand.QuietMode ParseQuiet(
        IReadOnlyDictionary<char, string> values)
        => ToQuietMode(ReadUInt32(values, 'q'));

    private static KgpParsedCommand.QuietMode ToQuietMode(uint value)
        => value switch
        {
            0 => KgpParsedCommand.QuietMode.Normal,
            1 => KgpParsedCommand.QuietMode.SuppressSuccess,
            _ => KgpParsedCommand.QuietMode.SuppressAll,
        };

    private static KgpParsedCommand.TransmissionData ParseTransmission(
        IReadOnlyDictionary<char, string> values)
        => new(
            ReadFormat(values),
            ReadMedium(values),
            ReadUInt32(values, 's'),
            ReadUInt32(values, 'v'),
            ReadUInt32(values, 'S'),
            ReadUInt32(values, 'O'),
            ReadUInt32(values, 'i'),
            ReadUInt32(values, 'I'),
            ReadUInt32(values, 'p'),
            values.ContainsKey('o')
                ? KgpParsedCommand.CompressionMode.Zlib
                : KgpParsedCommand.CompressionMode.None,
            ReadUInt32(values, 'm') != 0,
            ReadUInt32(values, 'N'));

    private static KgpParsedCommand.DisplayData ParseDisplay(
        IReadOnlyDictionary<char, string> values)
        => new(
            ReadUInt32(values, 'i'),
            ReadUInt32(values, 'I'),
            ReadUInt32(values, 'p'),
            ReadUInt32(values, 'x'),
            ReadUInt32(values, 'y'),
            ReadUInt32(values, 'w'),
            ReadUInt32(values, 'h'),
            ReadUInt32(values, 'X'),
            ReadUInt32(values, 'Y'),
            ReadUInt32(values, 'c'),
            ReadUInt32(values, 'r'),
            ReadUInt32(values, 'C') == 1,
            ReadUInt32(values, 'U') != 0,
            ReadInt32(values, 'z'),
            ReadUInt32(values, 'P'),
            ReadUInt32(values, 'Q'),
            ReadInt32(values, 'H'),
            ReadInt32(values, 'V'));

    private static KgpParsedCommand.DeleteSelector ParseDelete(
        IReadOnlyDictionary<char, string> values)
    {
        var target = values.TryGetValue('d', out var rawTarget)
            ? rawTarget[0]
            : 'a';

        return target switch
        {
            'a' => new KgpParsedCommand.DeleteSelector.All(false),
            'A' => new KgpParsedCommand.DeleteSelector.All(true),
            'i' => new KgpParsedCommand.DeleteSelector.ById(
                false,
                ReadUInt32(values, 'i'),
                ReadUInt32(values, 'p')),
            'I' => new KgpParsedCommand.DeleteSelector.ById(
                true,
                ReadUInt32(values, 'i'),
                ReadUInt32(values, 'p')),
            'n' => new KgpParsedCommand.DeleteSelector.ByNumber(
                false,
                ReadUInt32(values, 'I'),
                ReadUInt32(values, 'p')),
            'N' => new KgpParsedCommand.DeleteSelector.ByNumber(
                true,
                ReadUInt32(values, 'I'),
                ReadUInt32(values, 'p')),
            'c' => new KgpParsedCommand.DeleteSelector.AtCursor(false),
            'C' => new KgpParsedCommand.DeleteSelector.AtCursor(true),
            'f' => new KgpParsedCommand.DeleteSelector.AnimationFrames(
                false,
                ReadUInt32(values, 'i'),
                ReadUInt32(values, 'I'),
                ReadUInt32(values, 'r')),
            'F' => new KgpParsedCommand.DeleteSelector.AnimationFrames(
                true,
                ReadUInt32(values, 'i'),
                ReadUInt32(values, 'I'),
                ReadUInt32(values, 'r')),
            'p' => new KgpParsedCommand.DeleteSelector.AtCell(
                false,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y')),
            'P' => new KgpParsedCommand.DeleteSelector.AtCell(
                true,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y')),
            'q' => new KgpParsedCommand.DeleteSelector.AtCellWithZIndex(
                false,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y'),
                ReadInt32(values, 'z')),
            'Q' => new KgpParsedCommand.DeleteSelector.AtCellWithZIndex(
                true,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y'),
                ReadInt32(values, 'z')),
            'r' => new KgpParsedCommand.DeleteSelector.ByRange(
                false,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y')),
            'R' => new KgpParsedCommand.DeleteSelector.ByRange(
                true,
                ReadUInt32(values, 'x'),
                ReadUInt32(values, 'y')),
            'x' => new KgpParsedCommand.DeleteSelector.ByColumn(
                false,
                ReadUInt32(values, 'x')),
            'X' => new KgpParsedCommand.DeleteSelector.ByColumn(
                true,
                ReadUInt32(values, 'x')),
            'y' => new KgpParsedCommand.DeleteSelector.ByRow(
                false,
                ReadUInt32(values, 'y')),
            'Y' => new KgpParsedCommand.DeleteSelector.ByRow(
                true,
                ReadUInt32(values, 'y')),
            'z' => new KgpParsedCommand.DeleteSelector.ByZIndex(
                false,
                ReadInt32(values, 'z')),
            'Z' => new KgpParsedCommand.DeleteSelector.ByZIndex(
                true,
                ReadInt32(values, 'z')),
            _ => throw new InvalidOperationException($"Unsupported KGP delete target: {target}."),
        };
    }

    private static KgpParsedCommand.AnimationFrameData ParseAnimationFrame(
        IReadOnlyDictionary<char, string> values)
        => new(
            ReadUInt32(values, 'x'),
            ReadUInt32(values, 'y'),
            ReadUInt32(values, 'c'),
            ReadUInt32(values, 'r'),
            ReadInt32(values, 'z'),
            ReadUInt32(values, 'X') == 1
                ? KgpParsedCommand.CompositionMode.Overwrite
                : KgpParsedCommand.CompositionMode.AlphaBlend,
            ReadUInt32(values, 'Y'));

    private static KgpParsedCommand.AnimationControlData ParseAnimationControl(
        IReadOnlyDictionary<char, string> values)
        => new(
            ReadUInt32(values, 'i'),
            ReadUInt32(values, 'I'),
            ReadUInt32(values, 'p'),
            ReadUInt32(values, 's') switch
            {
                1 => KgpParsedCommand.AnimationPlaybackState.Stopped,
                2 => KgpParsedCommand.AnimationPlaybackState.Loading,
                3 => KgpParsedCommand.AnimationPlaybackState.Running,
                _ => KgpParsedCommand.AnimationPlaybackState.None,
            },
            ReadUInt32(values, 'v'),
            ReadUInt32(values, 'c'),
            ReadUInt32(values, 'r'),
            ReadInt32(values, 'z'));

    private static KgpParsedCommand.CompositionData ParseComposition(
        IReadOnlyDictionary<char, string> values)
        => new(
            ReadUInt32(values, 'i'),
            ReadUInt32(values, 'I'),
            ReadUInt32(values, 'p'),
            ReadUInt32(values, 'c'),
            ReadUInt32(values, 'r'),
            ReadUInt32(values, 'x'),
            ReadUInt32(values, 'y'),
            ReadUInt32(values, 'w'),
            ReadUInt32(values, 'h'),
            ReadUInt32(values, 'X'),
            ReadUInt32(values, 'Y'),
            ReadUInt32(values, 'C') == 0
                ? KgpParsedCommand.CompositionMode.AlphaBlend
                : KgpParsedCommand.CompositionMode.Overwrite);

    private static KgpFormat ReadFormat(IReadOnlyDictionary<char, string> values)
    {
        if (!values.TryGetValue('f', out var value))
            return KgpFormat.Rgba32;

        _ = TryParseFormat(value, out var format);
        return format;
    }

    private static KgpTransmissionMedium ReadMedium(
        IReadOnlyDictionary<char, string> values)
    {
        if (!values.TryGetValue('t', out var value))
            return KgpTransmissionMedium.Direct;

        _ = TryParseMedium(value, out var medium);
        return medium;
    }

    private static uint ReadUInt32(
        IReadOnlyDictionary<char, string> values,
        char key)
    {
        if (!values.TryGetValue(key, out var value))
            return 0;

        _ = TryParseUInt32(value, out var result);
        return result;
    }

    private static int ReadInt32(
        IReadOnlyDictionary<char, string> values,
        char key)
    {
        if (!values.TryGetValue(key, out var value))
            return 0;

        _ = TryParseInt32(value, out var result);
        return result;
    }

    private static bool TryParseAction(string value, out KgpAction action)
    {
        action = value switch
        {
            "t" => KgpAction.Transmit,
            "T" => KgpAction.TransmitAndDisplay,
            "q" => KgpAction.Query,
            "p" => KgpAction.Put,
            "d" => KgpAction.Delete,
            "f" => KgpAction.AnimationFrame,
            "a" => KgpAction.AnimationControl,
            "c" => KgpAction.Compose,
            _ => default,
        };
        return value is "t" or "T" or "q" or "p" or "d" or "f" or "a" or "c";
    }

    private static bool TryParseFormat(string value, out KgpFormat format)
    {
        format = value switch
        {
            "24" => KgpFormat.Rgb24,
            "32" => KgpFormat.Rgba32,
            "100" => KgpFormat.Png,
            _ => default,
        };
        return value is "24" or "32" or "100";
    }

    private static bool TryParseMedium(
        string value,
        out KgpTransmissionMedium medium)
    {
        medium = value switch
        {
            "d" => KgpTransmissionMedium.Direct,
            "f" => KgpTransmissionMedium.File,
            "t" => KgpTransmissionMedium.TempFile,
            "s" => KgpTransmissionMedium.SharedMemory,
            _ => default,
        };
        return value is "d" or "f" or "t" or "s";
    }

    private static bool TryParseDeleteTarget(
        string value,
        out KgpDeleteTarget target)
    {
        target = value switch
        {
            "a" => KgpDeleteTarget.All,
            "A" => KgpDeleteTarget.AllFreeData,
            "i" => KgpDeleteTarget.ById,
            "I" => KgpDeleteTarget.ByIdFreeData,
            "n" => KgpDeleteTarget.ByNumber,
            "N" => KgpDeleteTarget.ByNumberFreeData,
            "c" => KgpDeleteTarget.AtCursor,
            "C" => KgpDeleteTarget.AtCursorFreeData,
            "p" => KgpDeleteTarget.AtCell,
            "P" => KgpDeleteTarget.AtCellFreeData,
            "q" => KgpDeleteTarget.AtCellWithZIndex,
            "Q" => KgpDeleteTarget.AtCellWithZIndexFreeData,
            "x" => KgpDeleteTarget.ByColumn,
            "X" => KgpDeleteTarget.ByColumnFreeData,
            "y" => KgpDeleteTarget.ByRow,
            "Y" => KgpDeleteTarget.ByRowFreeData,
            "z" => KgpDeleteTarget.ByZIndex,
            "Z" => KgpDeleteTarget.ByZIndexFreeData,
            "r" => KgpDeleteTarget.ByRange,
            "R" => KgpDeleteTarget.ByRangeFreeData,
            "f" => KgpDeleteTarget.AnimationFrames,
            "F" => KgpDeleteTarget.AnimationFramesFreeData,
            _ => default,
        };
        return value is
            "a" or "A" or "i" or "I" or "n" or "N" or "c" or "C" or
            "p" or "P" or "q" or "Q" or "x" or "X" or "y" or "Y" or
            "z" or "Z" or "r" or "R" or "f" or "F";
    }

    private static bool TryParseUInt32(string value, out uint result)
    {
        if (value.Length == 0 || !value.All(IsAsciiDigit))
        {
            result = 0;
            return false;
        }

        return uint.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryParseInt32(string value, out int result)
    {
        var digits = value;
        if (value[0] == '-')
        {
            if (value.Length == 1)
            {
                result = 0;
                return false;
            }

            digits = value[1..];
        }

        if (digits.Length == 0 || !digits.All(IsAsciiDigit))
        {
            result = 0;
            return false;
        }

        return int.TryParse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool IsAsciiLetter(char value)
        => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiDigit(char value)
        => value is >= '0' and <= '9';
}
