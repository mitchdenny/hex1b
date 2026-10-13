using System.Globalization;
using System.Text;

namespace Hex1b;

internal static class TerminalProgramStatusCodec
{
    internal const int MaximumSequenceBytes = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static bool TryParse(string body, int sequenceBytes, out TerminalProgramStatusRecord? record,
        out string? clearId)
    {
        record = null;
        clearId = null;
        if (sequenceBytes > MaximumSequenceBytes || Encoding.UTF8.GetByteCount(body) + 8 > MaximumSequenceBytes)
            return false;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in body.Split(':'))
        {
            var equal = pair.IndexOf('=');
            var key = (equal < 0 ? pair : pair[..equal]).Trim();
            var value = equal < 0 ? "" : pair[(equal + 1)..].Trim();
            // Limits apply even to unknown, duplicate, or otherwise malformed pairs.
            if (Encoding.UTF8.GetByteCount(key) > 16 ||
                key == "id" && (Encoding.UTF8.GetByteCount(value) > 128 ||
                    value.Split('/').Length > 8 || value.Split('/').Any(segment => Encoding.UTF8.GetByteCount(segment) > 32)) ||
                key == "app" && Encoding.UTF8.GetByteCount(value) > 32 ||
                key == "msg" && Encoding.UTF8.GetByteCount(value) > 2732 ||
                key == "title" && Encoding.UTF8.GetByteCount(value) > 256)
                return false;
            if (equal < 0 || key.Length == 0 || key.Any(c => c is < 'a' or > 'z') ||
                value.Any(c => !IsValueCharacter(c)))
                continue;
            if (key is "msg" or "title" && !TryDecode(value, key == "msg" ? 2048 : 192, out _))
                return false;
            if (key is "state" or "id" or "kind" or "progress" or "app" or "title" or "msg")
                values[key] = value;
        }

        var id = values.GetValueOrDefault("id") ?? "";
        if (values.ContainsKey("id") && !IsValidId(id))
            return false;
        var stateText = values.GetValueOrDefault("state");
        if (stateText == "clear")
        {
            clearId = id;
            return true;
        }
        if (!TryState(stateText, out var state))
            return false;
        TerminalProgramStatusKind? kind = state == TerminalProgramStatusState.Blocked
            ? values.GetValueOrDefault("kind") switch
            {
                "permission" => TerminalProgramStatusKind.Permission,
                "question" => TerminalProgramStatusKind.Question,
                "auth" => TerminalProgramStatusKind.Auth,
                _ => null
            } : null;
        int? progress = null;
        var progressText = values.GetValueOrDefault("progress");
        if (state is TerminalProgramStatusState.Working or TerminalProgramStatusState.Blocked &&
            progressText is { Length: > 0 } && progressText.All(c => c is >= '0' and <= '9') &&
            int.TryParse(progressText, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= 100)
            progress = number;
        var app = values.GetValueOrDefault("app");
        if (app is not null && !IsSegment(app))
            app = null;
        string? title = null, message = null;
        if (values.TryGetValue("title", out var encodedTitle))
            TryDecode(encodedTitle, 192, out title);
        if (values.TryGetValue("msg", out var encodedMessage))
            TryDecode(encodedMessage, 2048, out message);
        record = new(id, state, kind, progress, app, title, message);
        return true;
    }

    internal static string Serialize(TerminalProgramStatusRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!Enum.IsDefined(record.State) || record.Kind is { } kind && !Enum.IsDefined(kind) ||
            record.Progress is < 0 or > 100 || record.Id is null ||
            record.Id.Length != 0 && !IsValidId(record.Id) ||
            record.App is not null && !IsSegment(record.App) ||
            record.Kind is not null && record.State != TerminalProgramStatusState.Blocked ||
            record.Progress is not null && record.State is not (TerminalProgramStatusState.Working or TerminalProgramStatusState.Blocked))
            throw new ArgumentException("Invalid program status report.", nameof(record));
        var body = "state=" + record.State.ToString().ToLowerInvariant();
        if (record.Id.Length != 0) body += ":id=" + record.Id;
        if (record.Kind is { } reportKind) body += ":kind=" + reportKind.ToString().ToLowerInvariant();
        if (record.Progress is { } progress) body += ":progress=" + progress.ToString(CultureInfo.InvariantCulture);
        if (record.App is { } app) body += ":app=" + app;
        if (record.Title is { } title) body += ":title=" + Encode(title, 192);
        if (record.Message is { } message) body += ":msg=" + Encode(message, 2048);
        return "\x1b]7501;" + body + "\x1b\\";
    }

    internal static string Serialize(TerminalProgramStatusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Serialize(new TerminalProgramStatusRecord(report.Id ?? "", report.State, report.Kind,
            report.Progress, report.App, report.Title, report.Message));
    }

    internal static string SerializeClear(string? id)
    {
        if (id is { Length: > 0 } && !IsValidId(id))
            throw new ArgumentException("Invalid program status id.", nameof(id));
        return "\x1b]7501;state=clear" + (string.IsNullOrEmpty(id) ? "" : ":id=" + id) + "\x1b\\";
    }

    private static string Encode(string text, int limit)
    {
        try
        {
            var bytes = StrictUtf8.GetBytes(text);
            if (bytes.Length > limit || text.Any(IsControl))
                throw new ArgumentException("Program status text exceeds its limit or contains controls.");
            return Convert.ToBase64String(bytes);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("Program status text must be valid UTF-8.", nameof(text), exception);
        }
    }

    private static bool TryDecode(string value, int limit, out string? text)
    {
        text = null;
        // Convert.FromBase64String accepts whitespace; the wire grammar does not.
        if (value.Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '=')))
            return false;
        var firstPadding = value.IndexOf('=');
        if (firstPadding >= 0 && (value.Length % 4 != 0 || value.Length - firstPadding > 2 ||
            value[firstPadding..].Any(c => c != '=')))
            return false;
        if (value.Length % 4 == 1)
            return false;
        try
        {
            var bytes = Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
            if (bytes.Length > limit) return false;
            text = StrictUtf8.GetString(bytes);
            return !text.Any(IsControl);
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsControl(char c) => c <= '\x1f' || c is >= '\x7f' and <= '\x9f';
    private static bool IsValueCharacter(char c)
        => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.' or ',' or '+' or '/' or '=' or '-';
    private static bool IsSegment(string value)
        => value.Length is >= 1 and <= 32 && value.All(c =>
            c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.' or '+' or '-');
    internal static bool IsValidId(string id)
        => id.Length is >= 1 and <= 128 && id.Split('/') is var segments &&
           segments.Length <= 8 && segments.All(IsSegment);
    private static bool TryState(string? value, out TerminalProgramStatusState state)
    {
        state = value switch
        {
            "idle" => TerminalProgramStatusState.Idle,
            "working" => TerminalProgramStatusState.Working,
            "blocked" => TerminalProgramStatusState.Blocked,
            "done" => TerminalProgramStatusState.Done,
            "error" => TerminalProgramStatusState.Error,
            _ => (TerminalProgramStatusState)(-1)
        };
        return Enum.IsDefined(state);
    }
}
