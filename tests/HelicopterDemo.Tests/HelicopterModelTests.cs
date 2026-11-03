using System.Security.Cryptography;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelicopterDemo.Tests;

[TestClass]
public class HelicopterModelTests
{
    [TestMethod]
    public void Load_BundledAsset_HasExpectedTopologyAndSeparateRotors()
    {
        var model = new HelicopterModel(HelicopterModel.AssetPath);
        Assert.AreEqual(1088, model.ImportedTriangleCount);
        Assert.AreEqual(48, model.MainRotor.Children.OfType<SceneMesh>().Sum(m => m.Geometry.PrimitiveCount));
        Assert.AreEqual(36, model.TailRotor.Children.OfType<SceneMesh>().Sum(m => m.Geometry.PrimitiveCount));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(HelicopterModel.AssetPath)));
        Assert.AreEqual("2FA5851D814F87C11B8E066C3E06C0DDEA6F9CB8689E22573D5AC3A10C0ACAA2", hash);
    }

    [TestMethod]
    public void Update_RotorsSpinAroundTheirPivots_WithoutReplacingGeometry()
    {
        var model = new HelicopterModel(HelicopterModel.AssetPath);
        var flight = new FlightState();
        model.Update(flight);
        var mainPosition = model.MainRotor.GetPositionWorldSpace();
        var tailPosition = model.TailRotor.GetPositionWorldSpace();
        var mainRotation = model.MainRotor.Rotation;
        var tailRotation = model.TailRotor.Rotation;
        var geometry = model.MainRotor.Children.OfType<SceneMesh>().Single().Geometry;
        var positions = geometry.GetAttribute("position");
        flight.Step(0.1f);
        model.Update(flight);

        Assert.AreEqual(mainPosition, model.MainRotor.GetPositionWorldSpace());
        Assert.AreEqual(tailPosition, model.TailRotor.GetPositionWorldSpace());
        Assert.AreNotEqual(mainRotation, model.MainRotor.Rotation);
        Assert.AreNotEqual(tailRotation, model.TailRotor.Rotation);
        Assert.AreSame(positions, geometry.GetAttribute("position"));
    }

    [TestMethod]
    public void Update_MouseTilt_BanksTowardScreenDirectionRegardlessOfHeading()
    {
        var model = new HelicopterModel(HelicopterModel.AssetPath);
        var flight = new FlightState();
        flight.AimAt(100, 50, 101, 101);
        model.Update(flight);
        var firstUp = model.Root.Rotation.RotateVector(Vector3.UnitY);
        Assert.IsGreaterThan(0.3f, firstUp.X);
        Assert.AreEqual(0, firstUp.Z, 0.0001f);
        flight.Turn(MathF.PI / 2);
        model.Update(flight);
        var turnedUp = model.Root.Rotation.RotateVector(Vector3.UnitY);
        Assert.AreEqual(firstUp.X, turnedUp.X, 0.0001f);
        Assert.AreEqual(firstUp.Z, turnedUp.Z, 0.0001f);
        Assert.AreEqual(FlightState.InitialAltitude + HelicopterModel.GroundClearance, model.Root.Position.Y);
    }

    [TestMethod]
    public void Update_ClimbingAndLanding_MovesModelVerticallyAndLevelsAtGroundContact()
    {
        var model = new HelicopterModel(HelicopterModel.AssetPath);
        var flight = new FlightState();
        model.Update(flight);
        var initialY = model.Root.Position.Y;
        flight.ChangeRotorSpeed(1);
        flight.Step(1);
        model.Update(flight);
        Assert.IsGreaterThan(initialY, model.Root.Position.Y);
        Assert.AreEqual(flight.Altitude + HelicopterModel.GroundClearance, model.Root.Position.Y);
        flight.AimAt(100, 50, 101, 101);
        flight.ChangeRotorSpeed(-100);
        flight.Step(10);
        model.Update(flight);
        Assert.IsTrue(flight.IsGrounded);
        Assert.AreEqual(HelicopterModel.GroundClearance, model.Root.Position.Y);
        Assert.AreEqual(Vector3.UnitY, model.Root.Rotation.RotateVector(Vector3.UnitY));
    }
}
