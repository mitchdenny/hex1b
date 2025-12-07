namespace Hex1b.Theming;

/// <summary>
/// Represents a theme element with a typed value.
/// </summary>
public class Hex1bThemeElement<T>
{
    public string Name { get; }
    public Func<T> DefaultValue { get; }

    public Hex1bThemeElement(string name, Func<T> defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public override string ToString() => Name;
    
    public override int GetHashCode() => Name.GetHashCode();
    
    public override bool Equals(object? obj) => 
        obj is Hex1bThemeElement<T> other && Name == other.Name;
}
