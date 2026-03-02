namespace LogicBuilderDemo.Models;

/// <summary>
/// Something that can be placed on a track (a command or a saved sequence).
/// </summary>
public interface ITrackStep
{
    string Name { get; }
    string Glyph { get; }
    int Cost { get; }
    string Display { get; }
}

/// <summary>
/// A track is a named sequence of steps that can be executed.
/// </summary>
public class Track
{
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public string Name { get; set; }
    public List<ITrackStep> Steps { get; } = [];
    public bool IsFromPalette { get; set; }

    public int TotalCost => Steps.Sum(s => s.Cost);

    public Track(string name)
    {
        Name = name;
    }
}
