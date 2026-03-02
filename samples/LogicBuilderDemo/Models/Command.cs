namespace LogicBuilderDemo.Models;

/// <summary>
/// A LOGO turtle command that can be placed on a track.
/// Rendered as: gNAME# where g=glyph, #=cost.
/// </summary>
public record Command(string Name, string Glyph, int Cost) : ITrackStep
{
    public static readonly Command Forward = new("Fwd", "^", 1);
    public static readonly Command TurnLeft = new("Left", "<", 1);
    public static readonly Command TurnRight = new("Right", ">", 1);
    public static readonly Command PenUp = new("PenUp", ".", 0);
    public static readonly Command PenDownCmd = new("PenDn", "#", 0);

    public static IReadOnlyList<Command> Defaults =>
    [
        Forward,
        TurnLeft,
        TurnRight,
        PenUp,
        PenDownCmd,
    ];

    /// <summary>Renders as gNAME# e.g. "^Fwd1"</summary>
    public string Display => $"{Glyph}{Name}{Cost}";

    public override string ToString() => Display;
}
