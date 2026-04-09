namespace Hex1b.Widgets;

/// <summary>
/// Tracks form fields registered during form construction.
/// Used to resolve cross-field references for validation and enablement.
/// </summary>
public sealed class FormFieldRegistry
{
    private readonly List<string> _fieldIds = new();

    /// <summary>
    /// Registers a field and returns its unique ID.
    /// </summary>
    internal string RegisterField(string label)
    {
        var fieldId = $"field_{_fieldIds.Count}_{label}";
        _fieldIds.Add(fieldId);
        return fieldId;
    }

    /// <summary>
    /// Gets all registered field IDs.
    /// </summary>
    public IReadOnlyList<string> FieldIds => _fieldIds;
}
