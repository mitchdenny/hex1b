namespace Hex1b.Theming;

/// <summary>
/// Represents a theme element with a typed value.
/// </summary>
public class Hex1bThemeElement<T>
{
    public string Name { get; }
    public Func<T> DefaultValue { get; }
    
    /// <summary>
    /// Optional fallback element to check before using DefaultValue.
    /// Enables cascading theme values (e.g., TopLine falls back to HorizontalLine).
    /// </summary>
    public Hex1bThemeElement<T>? Fallback { get; }

    public Hex1bThemeElement(string name, Func<T> defaultValue,
                             Hex1bThemeElement<T>? fallback = null)
    {
        Name = name;
        DefaultValue = defaultValue;
        Fallback = fallback;
    }

    public override string ToString() => Name;
    
    public override int GetHashCode() => Name.GetHashCode();
    
    public override bool Equals(object? obj) => 
        obj is Hex1bThemeElement<T> other && Name == other.Name;
}
