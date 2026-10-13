namespace Hex1b;

/// <summary>Captures an immutable inventory of program reports for one terminal.</summary>
public sealed class TerminalProgramStatusSnapshot
{
    /// <summary>Creates an inventory, preserving its least-recently-updated ordering.</summary>
    /// <param name="records">The records to copy. Ids must be unique.</param>
    /// <exception cref="ArgumentNullException">The records or a record id is null.</exception>
    /// <exception cref="ArgumentException">The inventory contains duplicate ids or more than 256 records.</exception>
    public TerminalProgramStatusSnapshot(IEnumerable<TerminalProgramStatusRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var inventory = new List<TerminalProgramStatusRecord>();
        var byId = new Dictionary<string, TerminalProgramStatusRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(record.Id);
            if (inventory.Count == 256 || !byId.TryAdd(record.Id, record))
                throw new ArgumentException("An inventory requires unique ids and at most 256 records.", nameof(records));
            inventory.Add(record);
        }
        for (var i = 0; i < inventory.Count; i++)
        {
            var record = inventory[i];
            var app = record.App;
            var ancestor = record.Id;
            while (app is null && ancestor.Length > 0)
            {
                var slash = ancestor.LastIndexOf('/');
                ancestor = slash < 0 ? "" : ancestor[..slash];
                if (byId.TryGetValue(ancestor, out var parent))
                    app = parent.App;
            }
            inventory[i] = record with { EffectiveApp = app };
        }
        Records = inventory.AsReadOnly();
    }

    /// <summary>Gets the records in least-recently-updated order, including the root if present.</summary>
    public IReadOnlyList<TerminalProgramStatusRecord> Records { get; }

    /// <summary>Finds the record for an exact, case-sensitive id.</summary>
    /// <param name="id">The id, or an empty string for the root.</param>
    /// <returns>The matching record, or null if absent.</returns>
    public TerminalProgramStatusRecord? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Records.FirstOrDefault(record => record.Id == id);
    }

    /// <summary>Gets all descendants of an id, excluding the addressed record itself.</summary>
    /// <param name="id">The ancestor id, or an empty string for all non-root records.</param>
    /// <returns>An immutable list in least-recently-updated order. The ancestor need not exist.</returns>
    public IReadOnlyList<TerminalProgramStatusRecord> GetDescendants(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Array.AsReadOnly(Records.Where(record => IsDescendant(record.Id, id)).ToArray());
    }

    internal static bool IsDescendant(string candidate, string id)
        => candidate.Length > id.Length && (id.Length == 0 || candidate.StartsWith(id + "/", StringComparison.Ordinal));

    internal bool ContentEquals(TerminalProgramStatusSnapshot other) => Records.SequenceEqual(other.Records);
}
