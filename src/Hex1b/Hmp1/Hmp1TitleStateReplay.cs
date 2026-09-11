using System.Text;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b;

internal static class Hmp1TitleStateReplay
{
    internal static string Build(Hex1bTerminalSnapshot snapshot, int remainingBytes)
    {
        var replay = new StringBuilder();
        // Stack snapshots are top-first. Reconstruct from the bottom, then restore
        // the current values, which may differ from the last saved pair.
        for (var i = snapshot.SavedTitles.Count - 1; i >= 0; i--)
        {
            var saved = snapshot.SavedTitles[i];
            Append("2", saved.Title);
            Append("1", saved.IconName);
            Append("22", "");
        }
        Append("2", snapshot.WindowTitle);
        Append("1", snapshot.IconName);
        return replay.ToString();

        void Append(string command, string payload)
        {
            var sequence = AnsiTokenSerializer.Serialize(new OscToken(command, "", payload, UseEscBackslash: true));
            var length = Encoding.UTF8.GetByteCount(sequence);
            if (length > remainingBytes)
                throw new InvalidDataException("Terminal title state exceeds the HMP1 StateSync payload limit.");
            remainingBytes -= length;
            replay.Append(sequence);
        }
    }
}
