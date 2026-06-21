namespace DirtRaceDemo.Game;

/// <summary>
/// Formats the heads-up display lines for the racer.
/// </summary>
public static class RaceHud
{
    public static string StatusLine(RaceGame game)
    {
        var surface = game.OnTrack ? "Track" : "Dirt";
        var air = game.Airborne ? $" | AIR {game.AirTime:0.0}s" : string.Empty;
        var best = game.BestLapTime > 0.0f ? $" | Best {game.BestLapTime:0.0}s" : string.Empty;
        return $" Lap {game.Lap} | Speed {game.SpeedDisplay:0} | {surface}{air}{best} ";
    }

    public static string ControlsLine()
    {
        return " W/Up accelerate   S/Down brake   A/Left  D/Right steer   Space handbrake   R reset   Q/Esc quit ";
    }
}
