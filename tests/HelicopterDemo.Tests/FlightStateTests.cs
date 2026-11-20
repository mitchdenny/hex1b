using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelicopterDemo.Tests;

[TestClass]
public class FlightStateTests
{
    [TestMethod]
    public void Step_AfterDirectionalInput_MovesWithDampedMomentum()
    {
        var flight = new FlightState();
        flight.Nudge(new Vector2(0, -1));
        var initialSpeed = flight.Velocity.Length();
        flight.Step(0.5f);

        Assert.IsLessThan(0, flight.Position.Y);
        Assert.AreEqual(0, flight.Position.X);
        Assert.IsTrue(flight.Velocity.Length() is > 0 && flight.Velocity.Length() < initialSpeed);
        Assert.IsGreaterThan(0, flight.RotorAngle);
    }

    [TestMethod]
    public void Step_DifferentFrameRates_ProducesEquivalentFlight()
    {
        var oneFrame = new FlightState();
        var manyFrames = new FlightState();
        oneFrame.Nudge(new Vector2(1, -1));
        manyFrames.Nudge(new Vector2(1, -1));
        oneFrame.Step(1);
        for (var i = 0; i < 40; i++)
            manyFrames.Step(0.025f);

        Assert.AreEqual(oneFrame.Position.X, manyFrames.Position.X, 0.0001f);
        Assert.AreEqual(oneFrame.Position.Y, manyFrames.Position.Y, 0.0001f);
        Assert.AreEqual(oneFrame.Velocity.X, manyFrames.Velocity.X, 0.0001f);
        Assert.AreEqual(oneFrame.RotorAngle, manyFrames.RotorAngle, 0.0001f);
    }

    [TestMethod]
    public void Nudge_RepeatedDiagonalInput_CapsTotalSpeed()
    {
        var flight = new FlightState();
        for (var i = 0; i < 100; i++)
            flight.Nudge(new Vector2(1, -1));
        Assert.AreEqual(FlightState.MaximumSpeed, flight.Velocity.Length(), 0.0001f);
    }

    [TestMethod]
    public void Step_AtMapEdge_StopsOutwardVelocity()
    {
        var flight = new FlightState();
        for (var i = 0; i < 1000; i++)
        {
            flight.Nudge(new Vector2(1, 1));
            flight.Step(0.1f);
        }
        Assert.AreEqual(new Vector2(FlightState.WorldLimit), flight.Position);
        Assert.AreEqual(Vector2.Zero, flight.Velocity);
    }

    [TestMethod]
    public void Step_Paused_IgnoresFlightInputAndFreezesRotors()
    {
        var flight = new FlightState();
        flight.Nudge(new Vector2(0, -1));
        flight.Step(0.1f);
        var position = flight.Position;
        var velocity = flight.Velocity;
        var rotor = flight.RotorAngle;
        flight.Paused = true;
        flight.Nudge(Vector2.One);
        flight.Turn(1);
        flight.Step(10);

        Assert.AreEqual(position, flight.Position);
        Assert.AreEqual(velocity, flight.Velocity);
        Assert.AreEqual(rotor, flight.RotorAngle);
        Assert.AreEqual(0, flight.Heading);
    }

    [TestMethod]
    public void Brake_WhileMoving_StopsWithoutResettingPosition()
    {
        var flight = new FlightState();
        flight.Nudge(Vector2.One);
        flight.Step(0.1f);
        var position = flight.Position;
        flight.Brake();
        flight.Step(1);
        Assert.AreEqual(position, flight.Position);
        Assert.AreEqual(Vector2.Zero, flight.Velocity);
    }

    [TestMethod]
    public void Reset_AfterFlight_RestoresInitialHover()
    {
        var flight = new FlightState();
        flight.Nudge(Vector2.One);
        flight.Turn(-0.2f);
        flight.Step(1);
        flight.Paused = true;
        flight.Reset();

        Assert.AreEqual(Vector2.Zero, flight.Position);
        Assert.AreEqual(Vector2.Zero, flight.Velocity);
        Assert.AreEqual(0, flight.Heading);
        Assert.AreEqual(0, flight.RotorAngle);
        Assert.IsFalse(flight.Paused);
    }

    [TestMethod]
    public void Turn_RepeatedRotations_KeepsHeadingWrapped()
    {
        var flight = new FlightState();
        for (var i = 0; i < 1000; i++)
            flight.Turn(-0.18f);
        Assert.IsTrue(flight.Heading is >= 0 && flight.Heading < MathF.Tau);
    }

    [TestMethod]
    public void Step_InvalidTime_Throws()
    {
        var flight = new FlightState();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => flight.Step(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => flight.Step(float.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => flight.Step(float.PositiveInfinity));
    }

    [TestMethod]
    [DataRow(0, 50, -1, 0)]
    [DataRow(100, 50, 1, 0)]
    [DataRow(50, 0, 0, -1)]
    [DataRow(50, 100, 0, 1)]
    public void AimAt_ViewportEdges_TiltsAndAcceleratesInScreenDirection(int x, int y, int dx, int dz)
    {
        var flight = new FlightState();
        flight.AimAt(x, y, 101, 101);
        Assert.AreEqual(new Vector2(dx, dz), flight.Tilt);
        flight.Step(0.5f);
        Assert.IsGreaterThan(0, Vector2.Dot(flight.Position, flight.Tilt));
        Assert.IsGreaterThan(0, Vector2.Dot(flight.Velocity, flight.Tilt));
    }

    [TestMethod]
    public void AimAt_CenterDeadZoneAndOutsideViewport_LevelsHelicopter()
    {
        var flight = new FlightState();
        flight.AimAt(100, 100, 101, 101);
        Assert.AreEqual(1, flight.Tilt.Length(), 0.0001f);
        flight.AimAt(52, 49, 101, 101);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
        flight.AimAt(100, 50, 101, 101);
        flight.AimAt(-1, -1, 101, 101);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
        flight.AimAt(0, 0, 1, 1);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
    }

    [TestMethod]
    public void Step_ContinuousTiltAndRotation_IsFrameRateIndependent()
    {
        var oneFrame = new FlightState();
        var manyFrames = new FlightState();
        oneFrame.AimAt(90, 20, 101, 101);
        manyFrames.AimAt(90, 20, 101, 101);
        oneFrame.SetRotationDirection(-1);
        manyFrames.SetRotationDirection(-1);
        oneFrame.Step(1);
        for (var i = 0; i < 40; i++)
            manyFrames.Step(0.025f);

        Assert.AreEqual(oneFrame.Position.X, manyFrames.Position.X, 0.0001f);
        Assert.AreEqual(oneFrame.Position.Y, manyFrames.Position.Y, 0.0001f);
        Assert.AreEqual(oneFrame.Velocity.X, manyFrames.Velocity.X, 0.0001f);
        Assert.AreEqual(oneFrame.Heading, manyFrames.Heading, 0.0001f);
        Assert.AreEqual(oneFrame.Altitude, manyFrames.Altitude, 0.0001f);
        Assert.AreEqual(oneFrame.VerticalVelocity, manyFrames.VerticalVelocity, 0.0001f);
        Assert.IsLessThanOrEqualTo(FlightState.MaximumSpeed, oneFrame.Velocity.Length());
    }

    [TestMethod]
    public void Step_RotationHeldThenReleased_StopsYawAndWrapsLongRightTurns()
    {
        var flight = new FlightState();
        flight.SetRotationDirection(-1);
        flight.Step(10);
        Assert.IsTrue(flight.Heading is > 0 && flight.Heading < MathF.Tau);
        var heading = flight.Heading;
        flight.SetRotationDirection(0);
        flight.Step(1);
        Assert.AreEqual(heading, flight.Heading);
    }

    [TestMethod]
    public void ChangeRotorSpeed_WheelSteps_AreBoundedAndChangeLiftWithoutChangingHorizontalInput()
    {
        var normal = new FlightState();
        var fast = new FlightState();
        normal.AimAt(80, 50, 101, 101);
        fast.AimAt(80, 50, 101, 101);
        fast.ChangeRotorSpeed(1000);
        Assert.AreEqual(FlightState.MaximumRotorSpeed, fast.RotorSpeed);
        normal.Step(0.1f);
        fast.Step(0.1f);
        Assert.AreEqual(normal.Position, fast.Position);
        Assert.AreEqual(normal.Velocity, fast.Velocity);
        Assert.AreEqual(normal.Tilt, fast.Tilt);
        Assert.IsGreaterThan(normal.Altitude, fast.Altitude);
        Assert.IsGreaterThan(normal.VerticalVelocity, fast.VerticalVelocity);
        Assert.AreNotEqual(normal.RotorAngle, fast.RotorAngle);
        fast.ChangeRotorSpeed(-1000);
        Assert.AreEqual(0, fast.RotorSpeed);
        var angle = fast.RotorAngle;
        fast.Step(0.1f);
        Assert.AreEqual(angle, fast.RotorAngle);
    }

    [TestMethod]
    public void Brake_WithSustainedTilt_StopsAccelerationAndYawUntilNewInput()
    {
        var flight = new FlightState();
        flight.AimAt(100, 50, 101, 101);
        flight.SetRotationDirection(1);
        flight.Step(0.1f);
        flight.Brake();
        var position = flight.Position;
        var heading = flight.Heading;
        flight.Step(1);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
        Assert.AreEqual(Vector2.Zero, flight.Velocity);
        Assert.AreEqual(position, flight.Position);
        Assert.AreEqual(heading, flight.Heading);
    }

    [TestMethod]
    public void Paused_MouseInput_IsIgnoredAndResetRestoresRotorSpeed()
    {
        var flight = new FlightState();
        flight.AimAt(100, 50, 101, 101);
        flight.SetRotationDirection(1);
        flight.ChangeRotorSpeed(2);
        flight.Paused = true;
        flight.AimAt(0, 0, 101, 101);
        flight.SetRotationDirection(-1);
        flight.ChangeRotorSpeed(2);
        flight.Step(1);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
        Assert.AreEqual(0, flight.RotationDirection);
        Assert.AreEqual(30, flight.RotorSpeed);
        Assert.AreEqual(0, flight.RotorAngle);
        flight.Reset();
        Assert.AreEqual(FlightState.DefaultRotorSpeed, flight.RotorSpeed);
        Assert.AreEqual(Vector2.Zero, flight.Tilt);
        Assert.AreEqual(0, flight.RotationDirection);
    }

    [TestMethod]
    public void Step_LevelAtHoverThrottle_MaintainsAltitude()
    {
        var flight = new FlightState();
        flight.Step(10);
        Assert.AreEqual(FlightState.InitialAltitude, flight.Altitude);
        Assert.AreEqual(0, flight.VerticalVelocity);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public void Step_AboveOrBelowHover_ClimbsOrDescends(int throttleSteps)
    {
        var flight = new FlightState();
        flight.ChangeRotorSpeed(throttleSteps);
        flight.Step(0.5f);
        Assert.IsGreaterThan(0, (flight.Altitude - FlightState.InitialAltitude) * throttleSteps);
        Assert.IsGreaterThan(0, flight.VerticalVelocity * throttleSteps);
    }

    [TestMethod]
    public void Step_IncreasingBankAtHover_LosesMoreAltitudeAndExtraThrottleCompensates()
    {
        var level = new FlightState();
        var gentle = new FlightState();
        var banked = new FlightState();
        var compensated = new FlightState();
        gentle.AimAt(75, 50, 101, 101);
        banked.AimAt(100, 50, 101, 101);
        compensated.AimAt(100, 50, 101, 101);
        compensated.ChangeRotorSpeed(1);
        level.Step(1);
        gentle.Step(1);
        banked.Step(1);
        compensated.Step(1);

        Assert.IsLessThan(level.Altitude, gentle.Altitude);
        Assert.IsLessThan(gentle.Altitude, banked.Altitude);
        Assert.IsLessThan(0, banked.VerticalVelocity);
        Assert.IsGreaterThan(FlightState.InitialAltitude, compensated.Altitude);
    }

    [TestMethod]
    public void Step_ClimbingAtDifferentFrameRates_ProducesEquivalentAltitude()
    {
        var oneFrame = new FlightState();
        var manyFrames = new FlightState();
        oneFrame.ChangeRotorSpeed(1);
        manyFrames.ChangeRotorSpeed(1);
        oneFrame.Step(1);
        for (var i = 0; i < 40; i++)
            manyFrames.Step(0.025f);

        Assert.AreEqual(oneFrame.Altitude, manyFrames.Altitude, 0.0001f);
        Assert.AreEqual(oneFrame.VerticalVelocity, manyFrames.VerticalVelocity, 0.0001f);
    }

    [TestMethod]
    public void Step_GroundAndCeiling_StopsOutwardVelocityAndAllowsTakeoffAndDescent()
    {
        var flight = new FlightState();
        flight.ChangeRotorSpeed(-100);
        flight.AimAt(100, 50, 101, 101);
        flight.Step(10);
        Assert.IsTrue(flight.IsGrounded);
        Assert.AreEqual(0, flight.Altitude);
        Assert.AreEqual(0, flight.VerticalVelocity);
        Assert.AreEqual(Vector2.Zero, flight.Velocity);
        var landedPosition = flight.Position;
        flight.Nudge(Vector2.One);
        flight.Step(1);
        Assert.AreEqual(landedPosition, flight.Position);

        flight.ChangeRotorSpeed(100);
        flight.Step(0.5f);
        Assert.IsFalse(flight.IsGrounded);
        Assert.IsGreaterThan(0, flight.Altitude);
        flight.Step(100);
        Assert.AreEqual(FlightState.MaximumAltitude, flight.Altitude);
        Assert.AreEqual(0, flight.VerticalVelocity);
        flight.Step(1);
        Assert.AreEqual(FlightState.MaximumAltitude, flight.Altitude);
        flight.ChangeRotorSpeed(-21); // 300% -> 90%.
        flight.Step(0.5f);
        Assert.IsLessThan(FlightState.MaximumAltitude, flight.Altitude);
        Assert.IsLessThan(0, flight.VerticalVelocity);
    }

    [TestMethod]
    public void Paused_WhileClimbing_FreezesAltitudeAndResetRestoresHover()
    {
        var flight = new FlightState();
        flight.ChangeRotorSpeed(1);
        flight.Step(1);
        var altitude = flight.Altitude;
        var speed = flight.VerticalVelocity;
        flight.Paused = true;
        flight.Step(100);
        Assert.AreEqual(altitude, flight.Altitude);
        Assert.AreEqual(speed, flight.VerticalVelocity);
        flight.Reset();
        Assert.AreEqual(FlightState.InitialAltitude, flight.Altitude);
        Assert.AreEqual(0, flight.VerticalVelocity);
        Assert.AreEqual(FlightState.DefaultRotorSpeed, flight.RotorSpeed);
    }

    [TestMethod]
    public void Brake_WhileClimbing_LeavesThrottleAndVerticalFlightUnderPlayerControl()
    {
        var flight = new FlightState();
        flight.ChangeRotorSpeed(1);
        flight.Step(0.5f);
        var altitude = flight.Altitude;
        var throttle = flight.RotorSpeed;
        flight.Brake();
        flight.Step(0.5f);
        Assert.AreEqual(throttle, flight.RotorSpeed);
        Assert.IsGreaterThan(altitude, flight.Altitude);
        Assert.IsGreaterThan(0, flight.VerticalVelocity);
    }
}
