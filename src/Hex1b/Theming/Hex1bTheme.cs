namespace Hex1b.Theming;

/// <summary>
/// A theme containing values for various UI elements.
/// </summary>
public class Hex1bTheme
{
    private readonly Dictionary<string, object> _values = new();
    private readonly string _name;
    private bool _isLocked;

    public Hex1bTheme(string name)
    {
        _name = name;
    }

    public string Name => _name;

    /// <summary>
    /// Gets whether this theme is locked and cannot be modified.
    /// </summary>
    public bool IsLocked => _isLocked;

    /// <summary>
    /// Locks this theme, preventing any further modifications.
    /// </summary>
    public Hex1bTheme Lock()
    {
        _isLocked = true;
        return this;
    }

    /// <summary>
    /// Gets the value for a theme element, or its default if not set.
    /// </summary>
    public T Get<T>(Hex1bThemeElement<T> element)
    {
        if (_values.TryGetValue(element.Name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return element.DefaultValue();
    }

    /// <summary>
    /// Sets a value for a theme element.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the theme is locked.</exception>
    public Hex1bTheme Set<T>(Hex1bThemeElement<T> element, T value)
    {
        if (_isLocked)
        {
            throw new InvalidOperationException(
                $"Cannot modify locked theme '{_name}'. Use Clone() to create a modifiable copy first.");
        }
        _values[element.Name] = value!;
        return this;
    }

    /// <summary>
    /// Creates a copy of this theme that can be modified.
    /// </summary>
    public Hex1bTheme Clone(string? newName = null)
    {
        var clone = new Hex1bTheme(newName ?? _name);
        foreach (var kvp in _values)
        {
            clone._values[kvp.Key] = kvp.Value;
        }
        return clone;
    }
}
