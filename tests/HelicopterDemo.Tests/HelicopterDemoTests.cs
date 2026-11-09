using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelicopterDemo.Tests;

[TestClass]
public class HelicopterDemoTests
{
    [TestMethod]
    [DataRow(80, 24)]
    [DataRow(120, 40)]
    public async Task Build_FlightControls_MoveTurnBrakePauseAndReset(int width, int height)
    {
        var demo = new HelicopterDemoApp();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => demo.Build(context))
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        try
        {
            using var initial = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("GUNSHIP | FLYING")
                    && s.ContainsText("Helicopter by Zsky")
                    && s.GetLineTrimmed(height - 2).Contains("Quit")
                    && s.GetLineTrimmed(height - 1).Contains("Mouse tilt"),
                    TimeSpan.FromSeconds(10), "flight scene and bottom controls")
                .Build().ApplyAsync(terminal, cts.Token);

            var previewPath = Path.Combine(AppContext.BaseDirectory, $"helicopter-{width}x{height}.svg");
            File.WriteAllText(previewPath, ((IHex1bTerminalRegion)initial).ToSvg(new TerminalSvgOptions
            {
                ShowCellGrid = false,
                ShowPixelGrid = false
            }));
            TestContext.Current.AddResultFile(previewPath);

            using var final = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => SceneChanged(initial, s, height), TimeSpan.FromSeconds(5), "rotors animate without input")
                .Key(Hex1bKey.W)
                .WaitUntil(s => s.ContainsText("North") && s.ContainsText("Z -"), TimeSpan.FromSeconds(5), "fly north")
                .Key(Hex1bKey.D)
                .WaitUntil(s => s.ContainsText("East") && !s.ContainsText("X 0.0"), TimeSpan.FromSeconds(5), "fly east")
                .Key(Hex1bKey.Q)
                .WaitUntil(s => s.ContainsText("Turn left"), TimeSpan.FromSeconds(5), "rotate helicopter")
                .Key(Hex1bKey.Spacebar)
                .WaitUntil(s => s.ContainsText("Speed 0.0") && s.ContainsText("Brake"), TimeSpan.FromSeconds(5), "brake")
                .Key(Hex1bKey.P)
                .WaitUntil(s => s.ContainsText("GUNSHIP | PAUSED") && s.ContainsText("Resume"), TimeSpan.FromSeconds(5), "pause")
                .Tab().Enter()
                .WaitUntil(s => s.ContainsText("GUNSHIP | FLYING") && s.ContainsText("X 0.0 Z 0.0")
                    && s.ContainsText("Ready"), TimeSpan.FromSeconds(5), "Reset button restores initial hover")
                .Tab().Tab().Tab().Enter()
                .WaitUntil(_ => demo.ViewHeight == 20, TimeSpan.FromSeconds(5), "Zoom + button")
                .Tab().Enter()
                .WaitUntil(_ => demo.ViewHeight == 22, TimeSpan.FromSeconds(5), "Zoom - button")
                .Key(Hex1bKey.Z)
                .WaitUntil(_ => demo.ViewHeight == 20, TimeSpan.FromSeconds(5), "zoom-in shortcut")
                .Key(Hex1bKey.X)
                .WaitUntil(_ => demo.ViewHeight == 22, TimeSpan.FromSeconds(5), "zoom-out shortcut")
                .Ctrl().Key(Hex1bKey.C)
                .Build().ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(80, 24)]
    [DataRow(120, 40)]
    public async Task Build_MouseFlight_TiltsTurnsWhileHeldAndAdjustsRotors(int width, int height)
    {
        var demo = new HelicopterDemoApp();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => demo.Build(context))
            .WithMouse()
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        var centerX = width / 2;
        var centerY = (height - 2) / 2;
        try
        {
            using var result = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Rotor 100%")
                    && s.GetLineTrimmed(height - 1).Contains("Mouse tilt"),
                    TimeSpan.FromSeconds(10), "mouse controls ready")
                .MouseMoveTo(width - 4, centerY)
                .WaitUntil(_ => demo.Flight.Tilt.X > 0.7f && demo.Flight.Velocity.X > 0.1f,
                    TimeSpan.FromSeconds(5), "pointer banks and accelerates east")
                .MouseMoveTo(centerX, centerY)
                .WaitUntil(_ => demo.Flight.Tilt == System.Numerics.Vector2.Zero,
                    TimeSpan.FromSeconds(5), "center dead zone levels helicopter")
                .MouseDown(MouseButton.Left)
                .WaitUntil(_ => demo.Flight.RotationDirection == 1 && demo.Flight.Heading > 0.05f,
                    TimeSpan.FromSeconds(5), "holding left button turns continuously")
                .ScrollUp()
                .WaitUntil(s => s.ContainsText("Rotor 110%") && demo.Flight.RotationDirection == 1,
                    TimeSpan.FromSeconds(5), "wheel adjusts speed while holding rotation")
                .MouseUp(MouseButton.Left)
                .WaitUntil(_ => demo.Flight.RotationDirection == 0,
                    TimeSpan.FromSeconds(5), "release stops rotation")
                .MouseDown(MouseButton.Right)
                .WaitUntil(_ => demo.Flight.RotationDirection == -1,
                    TimeSpan.FromSeconds(5), "right button turns opposite direction")
                .MouseMoveTo(centerX, height - 2)
                .WaitUntil(_ => demo.Flight.RotationDirection == 0
                    && demo.Flight.Tilt == System.Numerics.Vector2.Zero,
                    TimeSpan.FromSeconds(5), "leaving viewport releases steering")
                .MouseUp(MouseButton.Right)
                .MouseMoveTo(centerX, 3)
                .WaitUntil(_ => demo.Flight.Tilt.Y < -0.7f && demo.Flight.Velocity.Y < -0.1f,
                    TimeSpan.FromSeconds(5), "pointer accelerates north")
                .Key(Hex1bKey.Spacebar)
                .WaitUntil(s => s.ContainsText("Speed 0.0") && demo.Flight.Tilt == System.Numerics.Vector2.Zero,
                    TimeSpan.FromSeconds(5), "brake releases sustained tilt")
                .Key(Hex1bKey.Z)
                .WaitUntil(_ => demo.ViewHeight == 20 && demo.Flight.Velocity == System.Numerics.Vector2.Zero,
                    TimeSpan.FromSeconds(5), "stationary pointer does not restart flight after braking")
                .MouseMoveTo(2, height - 2).Click()
                .WaitUntil(s => s.ContainsText("X 0.0 Z 0.0") && s.ContainsText("Rotor 100%"),
                    TimeSpan.FromSeconds(5), "toolbar reset remains clickable")
                .Ctrl().Key(Hex1bKey.C)
                .Build().ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task Build_MouseRotorLimitsAndPause_PreservesSafeHoverAndToolbarInput()
    {
        var demo = new HelicopterDemoApp();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => demo.Build(context))
            .WithMouse()
            .WithHeadless()
            .WithDimensions(100, 30)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        try
        {
            using var paused = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Rotor 100%") && s.ContainsText("Mouse tilt"),
                    TimeSpan.FromSeconds(10), "mouse scene ready")
                .MouseMoveTo(50, 14)
                .ScrollDown(32)
                .WaitUntil(s => s.ContainsText("Rotor 0%"), TimeSpan.FromSeconds(5), "wheel clamps to stopped rotors")
                .ScrollUp(32)
                .WaitUntil(s => s.ContainsText("Rotor 300%"), TimeSpan.FromSeconds(5), "wheel clamps maximum rotor speed")
                .Key(Hex1bKey.P)
                .WaitUntil(s => s.ContainsText("PAUSED"), TimeSpan.FromSeconds(5), "pause")
                .Build().ApplyAsync(terminal, cts.Token);
            var rotorAngle = demo.Flight.RotorAngle;
            var heading = demo.Flight.Heading;
            var position = demo.Flight.Position;
            using var ignored = await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(50, 14)
                .ScrollDown()
                .MouseMoveTo(95, 3)
                .MouseDown(MouseButton.Left).MouseUp(MouseButton.Left)
                .Key(Hex1bKey.Z)
                .WaitUntil(s => s.ContainsText("PAUSED") && demo.ViewHeight == 20,
                    TimeSpan.FromSeconds(5), "mouse inputs ignored while paused")
                .Build().ApplyAsync(terminal, cts.Token);
            Assert.AreEqual(FlightState.MaximumRotorSpeed, demo.Flight.RotorSpeed);
            Assert.AreEqual(rotorAngle, demo.Flight.RotorAngle);
            Assert.AreEqual(heading, demo.Flight.Heading);
            Assert.AreEqual(position, demo.Flight.Position);
            Assert.AreEqual(System.Numerics.Vector2.Zero, demo.Flight.Tilt);

            using var final = await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.P)
                .WaitUntil(s => s.ContainsText("FLYING") && demo.Flight.Tilt == System.Numerics.Vector2.Zero,
                    TimeSpan.FromSeconds(5), "resuming does not restore stale tilt")
                .MouseMoveTo(2, 28)
                .ScrollDown()
                .Key(Hex1bKey.X)
                .WaitUntil(_ => demo.ViewHeight == 22, TimeSpan.FromSeconds(5), "toolbar wheel does not control rotors")
                .Build().ApplyAsync(terminal, cts.Token);
            Assert.AreEqual(FlightState.MaximumRotorSpeed, demo.Flight.RotorSpeed);
            using var reset = await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(2, 28).Click()
                .WaitUntil(s => s.ContainsText("Rotor 100%") && s.ContainsText("X 0.0 Z 0.0"),
                    TimeSpan.FromSeconds(5), "mouse reset restores rotor speed and neutral controls")
                .Ctrl().Key(Hex1bKey.C)
                .Build().ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(80, 24)]
    [DataRow(120, 40)]
    public async Task Build_MouseThrottle_ClimbsLosesLiftWhenBankedLandsAndTakesOff(int width, int height)
    {
        var demo = new HelicopterDemoApp();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => demo.Build(context))
            .WithMouse()
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        var centerX = width / 2;
        var centerY = (height - 1) / 2;
        try
        {
            using var climbing = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Altitude 2.4") && s.ContainsText("Hover 100% level")
                    && s.GetLineTrimmed(height - 1).Contains("Wheel lift"),
                    TimeSpan.FromSeconds(10), "altitude controls ready")
                .MouseMoveTo(centerX, centerY)
                .ScrollUp()
                .WaitUntil(s => s.ContainsText("Rotor 110%") && s.ContainsText("V/S +")
                    && demo.Flight.Altitude > FlightState.InitialAltitude + 1,
                    TimeSpan.FromSeconds(5), "above-hover throttle produces an actual climb")
                .Build().ApplyAsync(terminal, cts.Token);
            var previewPath = Path.Combine(AppContext.BaseDirectory, $"helicopter-climbing-{width}x{height}.svg");
            File.WriteAllText(previewPath, ((IHex1bTerminalRegion)climbing).ToSvg(new TerminalSvgOptions
            {
                ShowCellGrid = false,
                ShowPixelGrid = false
            }));
            TestContext.Current.AddResultFile(previewPath);

            using var final = await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(centerX, centerY)
                .ScrollDown()
                .MouseMoveTo(width - 4, centerY)
                .WaitUntil(s => s.ContainsText("Rotor 100%") && s.ContainsText("V/S -")
                    && demo.Flight.Tilt.X > 0.7f && demo.Flight.VerticalVelocity < -0.05f,
                    TimeSpan.FromSeconds(5), "banking at hover throttle eventually loses height")
                .MouseMoveTo(centerX, centerY)
                .ScrollDown(32)
                .WaitUntil(s => s.ContainsText("LANDED") && s.ContainsText("Altitude 0.0")
                    && s.ContainsText("Rotor 0%") && demo.Flight.VerticalVelocity == 0,
                    TimeSpan.FromSeconds(5), "cutting rotor throttle lands without sinking below ground")
                .ScrollUp(11)
                .WaitUntil(s => s.ContainsText("FLYING") && s.ContainsText("Rotor 110%")
                    && demo.Flight.Altitude > 0.2f && demo.Flight.VerticalVelocity > 0,
                    TimeSpan.FromSeconds(5), "increasing throttle takes off again")
                .ClickAt(2, height - 2)
                .WaitUntil(s => s.ContainsText("Altitude 2.4") && s.ContainsText("Rotor 100%")
                    && s.ContainsText("X 0.0 Z 0.0") && demo.Flight.VerticalVelocity == 0,
                    TimeSpan.FromSeconds(5), "reset restores initial level hover")
                .Ctrl().Key(Hex1bKey.C)
                .Build().ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task Program_RealTerminal_EnablesMotionAndRoutesMouseControls()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("dotnet", typeof(FlightState).Assembly.Location)
            .WithHeadless()
            .WithDimensions(100, 30)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        try
        {
            using var result = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.MouseProtocolAnyEnabled && s.MouseEncodingSgrEnabled
                    && s.ContainsText("Rotor 100%") && s.ContainsText("Mouse tilt"),
                    TimeSpan.FromSeconds(10), "actual entry point enables all-motion reporting")
                .MouseMoveTo(80, 14)
                .WaitUntil(s => s.ContainsText("GUNSHIP") && !s.GetLineTrimmed(0).Contains("X 0.0"),
                    TimeSpan.FromSeconds(5), "ordinary pointer motion accelerates the chopper")
                .MouseMoveTo(50, 14)
                .MouseDown(MouseButton.Left)
                .WaitUntil(s => s.ContainsText("Turn left"), TimeSpan.FromSeconds(5), "mouse press starts rotation")
                .ScrollUp()
                .WaitUntil(s => s.ContainsText("Rotor 110%"), TimeSpan.FromSeconds(5), "wheel during held turn")
                .MouseUp(MouseButton.Left)
                .Key(Hex1bKey.Spacebar)
                .WaitUntil(s => s.ContainsText("Speed 0.0"), TimeSpan.FromSeconds(5), "brake")
                .ClickAt(40, 28)
                .Build().ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static bool SceneChanged(Hex1bTerminalSnapshot before, Hex1bTerminalSnapshot after, int height)
    {
        for (var y = 2; y < height - 3; y++)
        {
            for (var x = 1; x < before.Width - 1; x++)
            {
                var a = before.GetCell(x, y);
                var b = after.GetCell(x, y);
                if (!Equals(a.Foreground, b.Foreground) || !Equals(a.Background, b.Background))
                    return true;
            }
        }
        return false;
    }
}
