namespace LogicBuilderDemo.Models;

/// <summary>
/// Cardinal directions the turtle can face.
/// </summary>
public enum Heading { Up, Right, Down, Left }

/// <summary>
/// A LOGO-style turtle that moves on a character grid, drawing lines as it goes.
/// Tracks arrival direction per cell to correctly draw corners when turning.
/// </summary>
public class Turtle
{
    private readonly Dictionary<(int X, int Y), char> _canvas = new();

    /// <summary>The heading the turtle had when it last arrived/drew at each cell.</summary>
    private readonly Dictionary<(int X, int Y), Heading> _arrivalHeading = new();

    public int X { get; private set; }
    public int Y { get; private set; }
    public Heading Heading { get; private set; } = Heading.Up;
    public bool PenDown { get; private set; } = true;
    public int StepsExecuted { get; private set; }

    /// <summary>The drawn characters on the canvas.</summary>
    public IReadOnlyDictionary<(int X, int Y), char> Canvas => _canvas;

    /// <summary>Arrow character representing the turtle's current heading.</summary>
    public char Arrow => Heading switch
    {
        Heading.Up => '^',
        Heading.Right => '>',
        Heading.Down => 'v',
        Heading.Left => '<',
        _ => '^'
    };

    public void Reset()
    {
        X = 0;
        Y = 0;
        Heading = Heading.Up;
        PenDown = true;
        StepsExecuted = 0;
        _canvas.Clear();
        _arrivalHeading.Clear();
    }

    public void Execute(ITrackStep step)
    {
        if (step is SavedSequence seq)
        {
            foreach (var inner in seq.Steps)
                Execute(inner);
            return;
        }

        if (step is not Command cmd) return;

        if (cmd == Command.Forward)
            MoveForward();
        else if (cmd == Command.TurnLeft)
            TurnLeft();
        else if (cmd == Command.TurnRight)
            TurnRight();
        else if (cmd == Command.PenUp)
            PenDown = false;
        else if (cmd == Command.PenDownCmd)
            PenDown = true;

        StepsExecuted++;
    }

    public void ExecuteTrack(Track track)
    {
        foreach (var step in track.Steps)
            Execute(step);
    }

    private void MoveForward()
    {
        if (PenDown)
        {
            var pos = (X, Y);
            if (_arrivalHeading.TryGetValue(pos, out var arrivedFrom) && arrivedFrom != Heading)
            {
                // Turtle arrived at this cell in one direction but is now departing
                // in another — draw a corner connecting the two
                _canvas[pos] = GetCornerChar(arrivedFrom, Heading);
            }
            else
            {
                // Straight line in current heading direction
                _canvas[pos] = GetStraightChar(Heading);
            }
            // Don't overwrite _arrivalHeading here — it must keep the original arrival direction
        }

        var (dx, dy) = Heading switch
        {
            Heading.Up => (0, -1),
            Heading.Right => (1, 0),
            Heading.Down => (0, 1),
            Heading.Left => (-1, 0),
            _ => (0, 0)
        };

        X += dx;
        Y += dy;

        // Record how the turtle arrived at the new cell
        // Only set if not already set (preserve original arrival direction for revisited cells)
        if (PenDown && !_arrivalHeading.ContainsKey((X, Y)))
            _arrivalHeading[(X, Y)] = Heading;
    }

    private void TurnLeft()
    {
        Heading = (Heading)(((int)Heading + 3) % 4);
    }

    private void TurnRight()
    {
        Heading = (Heading)(((int)Heading + 1) % 4);
    }

    /// <summary>
    /// Straight line character for the given heading.
    /// </summary>
    private static char GetStraightChar(Heading heading) => heading switch
    {
        Heading.Up or Heading.Down => '│',
        Heading.Left or Heading.Right => '─',
        _ => '·'
    };

    /// <summary>
    /// Corner character connecting the direction the turtle arrived from to where it's going.
    /// "from" = heading when entering the cell, "to" = heading when departing.
    /// Entry edge is opposite of travel direction (moving Up enters from bottom).
    /// Exit edge matches travel direction (departing Right exits through right).
    /// </summary>
    private static char GetCornerChar(Heading from, Heading to) =>
        (from, to) switch
        {
            // Arrived Up (enters bottom), departs Right (exits right) → bottom+right = ┌
            (Heading.Up, Heading.Right) => '┌',
            // Arrived Up (enters bottom), departs Left (exits left) → bottom+left = ┐
            (Heading.Up, Heading.Left) => '┐',
            // Arrived Down (enters top), departs Right (exits right) → top+right = └
            (Heading.Down, Heading.Right) => '└',
            // Arrived Down (enters top), departs Left (exits left) → top+left = ┘
            (Heading.Down, Heading.Left) => '┘',
            // Arrived Right (enters left), departs Up (exits top) → left+top = ┘
            (Heading.Right, Heading.Up) => '┘',
            // Arrived Right (enters left), departs Down (exits bottom) → left+bottom = ┐
            (Heading.Right, Heading.Down) => '┐',
            // Arrived Left (enters right), departs Up (exits top) → right+top = └
            (Heading.Left, Heading.Up) => '└',
            // Arrived Left (enters right), departs Down (exits bottom) → right+bottom = ┌
            (Heading.Left, Heading.Down) => '┌',
            // Same direction or U-turn
            _ => GetStraightChar(to)
        };
}
