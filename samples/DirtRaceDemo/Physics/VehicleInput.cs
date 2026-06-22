namespace DirtRaceDemo.Physics;

/// <summary>
/// A per-frame snapshot of driver intent in normalised ranges. Produced by the input
/// layer and consumed by <see cref="ArcadeVehicle"/>.
/// </summary>
public readonly record struct VehicleInput(float Throttle, float Steer, bool Handbrake)
{
    public static VehicleInput None => new(0.0f, 0.0f, false);
}
