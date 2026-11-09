using System.Text;
using Hex1b.Input;

namespace Hex1b.Tests;

[TestClass]
public class ConsoleMouseModeStateTests
{
    [TestMethod]
    [DataRow("", TerminalMouseTracking.None)]
    [DataRow("\x1b[?1006h", TerminalMouseTracking.None)]
    [DataRow("\x1b[?1000h", TerminalMouseTracking.Normal)]
    [DataRow("\x1b[?1002h", TerminalMouseTracking.Button)]
    [DataRow("\x1b[?1003h", TerminalMouseTracking.Any)]
    [DataRow("\x1b[?1000;1002;1003;1006h\x1b[?1003l", TerminalMouseTracking.Button)]
    [DataRow("\x1b[?1003;1006h\x1b[?1003l", TerminalMouseTracking.None)]
    [DataRow("\x1b[?1003;1006h\u001bc", TerminalMouseTracking.None)]
    [DataRow("\x1b[?1003;1006h\x1b[!p", TerminalMouseTracking.None)]
    [DataRow("\x1b[?1003$p", TerminalMouseTracking.None)]
    public void Observe_CompleteAndFragmentedWrites_TracksRequestedModes(
        string output, object expected)
    {
        var bytes = Encoding.UTF8.GetBytes(output);
        for (var split = 0; split <= bytes.Length; split++)
        {
            var state = new ConsoleMouseModeState();
            state.Observe(bytes.AsSpan(0, split));
            state.Observe(bytes.AsSpan(split));
            Assert.AreEqual((TerminalMouseTracking)expected, state.Tracking, $"Split at {split}");
        }
    }

    [TestMethod]
    [DataRow("\x1b]title;\x1b[?1003h\x07")]
    [DataRow("\x1b]title;\x1b[?1003h\x1b\\")]
    [DataRow("\x1bPq\x1b[?1003h\x1b\\")]
    [DataRow("\x1b_Gf=24;\x1b[?1003h\x1b\\")]
    [DataRow("\x1b^ignored\x1b[?1003h\x1b\\")]
    public void Observe_ControlStringPayload_DoesNotChangeMouseModes(string output)
    {
        var state = new ConsoleMouseModeState();
        foreach (var value in Encoding.UTF8.GetBytes(output))
            state.Observe(new byte[] { value });
        Assert.AreEqual(TerminalMouseTracking.None, state.Tracking);
        state.Observe("\x1b[?1003;1006h"u8);
        Assert.AreEqual(TerminalMouseTracking.Any, state.Tracking);
    }

    [TestMethod]
    public void Observe_DisableAndReenableInOneWrite_ReportsTransition()
    {
        var state = new ConsoleMouseModeState();
        Assert.IsTrue(state.Observe("\x1b[?1003h"u8));
        Assert.IsFalse(state.Observe("plain text"u8));
        Assert.IsTrue(state.Observe("\x1b[?1003l\x1b[?1003h"u8));
        Assert.AreEqual(TerminalMouseTracking.Any, state.Tracking);
    }

    [TestMethod]
    public void Observe_OverlongAndCancelledSequences_RecoversWithoutEnablingMouse()
    {
        var state = new ConsoleMouseModeState();
        state.Observe(Encoding.ASCII.GetBytes("\x1b[?" + new string('0', 1024) + ";1003h"));
        state.Observe("\x1b[?1003\x18h\x1b[?1003\x1ah"u8);
        Assert.AreEqual(TerminalMouseTracking.None, state.Tracking);
        state.Observe("\x1b[?1003h"u8);
        Assert.AreEqual(TerminalMouseTracking.Any, state.Tracking);
    }

    [TestMethod]
    [DataRow("", MouseAction.Down, TerminalMouseButton.Left, "")]
    [DataRow("\x1b[?1000;1006h", MouseAction.Move, TerminalMouseButton.None, "")]
    [DataRow("\x1b[?1002;1006h", MouseAction.Move, TerminalMouseButton.None, "")]
    [DataRow("\x1b[?1002;1006h", MouseAction.Drag, TerminalMouseButton.Left, "\x1b[<32;3;4M")]
    [DataRow("\x1b[?1003;1006h", MouseAction.Move, TerminalMouseButton.None, "\x1b[<35;3;4M")]
    [DataRow("\x1b[?1003;1006h", MouseAction.Up, TerminalMouseButton.Left, "\x1b[<0;3;4m")]
    [DataRow("\x1b[?1003;1006h", MouseAction.Down, TerminalMouseButton.WheelUp, "\x1b[<64;3;4M")]
    [DataRow("\x1b[?1000h", MouseAction.Down, TerminalMouseButton.Left, "\x1b[M #$")]
    public void InputModes_RequestedTrackingAndEncoding_UsesSharedWireContract(
        string output, MouseAction action, object button, string expected)
    {
        var state = new ConsoleMouseModeState();
        state.Observe(Encoding.ASCII.GetBytes(output));
        var actual = TerminalInputEncoder.EncodeMouse(
            new TerminalMouseInput((TerminalMouseButton)button, action, 2, 3, Hex1bModifiers.None), state.InputModes(80, 24));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(actual));
    }
}
