namespace DirtRaceDemo.Rendering;

using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// An isometric follow camera. Keeps the fixed 3/4 isometric orientation of
/// <see cref="SceneIsometricCamera"/> but smoothly tracks a focus point (the truck) so the
/// action stays centred. The camera sits back along its own view direction so everything in
/// front of it stays within the near/far range.
/// </summary>
public sealed class CameraRig
{
    private readonly Vector3 _forward;
    private readonly float _distance;
    private readonly float _followSpeed;

    private Vector3 _focus;

    public SceneIsometricCamera Camera { get; }

    public CameraRig(Vector3 initialFocus, float size = 26.0f, float distance = 40.0f, float followSpeed = 5.0f)
    {
        Camera = new SceneIsometricCamera("RaceCamera")
        {
            Size = size,
            Far = 400.0f,
        };

        _forward = Camera.GetForward().Normalized;
        _distance = distance;
        _followSpeed = followSpeed;
        _focus = initialFocus;
        ApplyPosition();
    }

    public void Update(Vector3 focus, float dt)
    {
        var t = 1.0f - MathF.Exp(-_followSpeed * dt);
        _focus = Vector3.Lerp(_focus, focus, t);
        ApplyPosition();
    }

    public void Snap(Vector3 focus)
    {
        _focus = focus;
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        Camera.Position = _focus - _forward * _distance;
    }
}
