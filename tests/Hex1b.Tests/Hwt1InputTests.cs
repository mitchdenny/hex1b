using System.Text.Json;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1InputTests
{
    [TestMethod]
    [DataRow("ArrowUp", "\x1b[A")]
    [DataRow("ArrowDown", "\x1b[B")]
    [DataRow("ArrowRight", "\x1b[C")]
    [DataRow("ArrowLeft", "\x1b[D")]
    [DataRow("Home", "\x1b[H")]
    [DataRow("End", "\x1b[F")]
    [DataRow("Insert", "\x1b[2~")]
    [DataRow("Delete", "\x1b[3~")]
    [DataRow("PageUp", "\x1b[5~")]
    [DataRow("PageDown", "\x1b[6~")]
    [DataRow("F1", "\x1bOP")]
    [DataRow("F2", "\x1bOQ")]
    [DataRow("F3", "\x1bOR")]
    [DataRow("F4", "\x1bOS")]
    [DataRow("F5", "\x1b[15~")]
    [DataRow("F6", "\x1b[17~")]
    [DataRow("F7", "\x1b[18~")]
    [DataRow("F8", "\x1b[19~")]
    [DataRow("F9", "\x1b[20~")]
    [DataRow("F10", "\x1b[21~")]
    [DataRow("F11", "\x1b[23~")]
    [DataRow("F12", "\x1b[24~")]
    [DataRow("Enter", "\r")]
    [DataRow("Backspace", "\x7f")]
    [DataRow("Tab", "\t")]
    [DataRow("Escape", "\x1b")]
    [DataRow("é", "é")]
    public void EncodeKey_NamedAndCharacterKeys_PreservesWireEncoding(string key, string expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        using var command = JsonDocument.Parse(JsonSerializer.Serialize(new { key }));

        Assert.AreEqual(expected, Hwt1Input.EncodeKey(command.RootElement, terminal));
    }

    [TestMethod]
    [DataRow("ArrowUp", Hex1bKey.UpArrow, "A")]
    [DataRow("ArrowDown", Hex1bKey.DownArrow, "B")]
    [DataRow("ArrowRight", Hex1bKey.RightArrow, "C")]
    [DataRow("ArrowLeft", Hex1bKey.LeftArrow, "D")]
    [DataRow("Home", Hex1bKey.Home, "H")]
    [DataRow("End", Hex1bKey.End, "F")]
    public void EncodeKey_CursorModesAndAllModifiers_MatchesTypedEncoder(
        string key, Hex1bKey typedKey, string final)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        foreach (var application in new[] { false, true })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?1{(application ? 'h' : 'l')}"));
            for (var modifier = 1; modifier <= 8; modifier++)
            {
                var shift = ((modifier - 1) & 1) != 0;
                var alt = ((modifier - 1) & 2) != 0;
                var ctrl = ((modifier - 1) & 4) != 0;
                using var command = JsonDocument.Parse(JsonSerializer.Serialize(new { key, shift, alt, ctrl }));
                var modifiers = (shift ? Hex1bModifiers.Shift : Hex1bModifiers.None) |
                    (alt ? Hex1bModifiers.Alt : Hex1bModifiers.None) |
                    (ctrl ? Hex1bModifiers.Control : Hex1bModifiers.None);
                var expected = modifier != 1 ? $"\x1b[1;{modifier}{final}" :
                    application ? $"\x1bO{final}" : $"\x1b[{final}";

                Assert.AreEqual(expected, Hwt1Input.EncodeKey(command.RootElement, terminal));
                Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
                    new Hex1bKeyEvent(typedKey, "", modifiers), terminal.InputModes));
            }
        }
    }

    [TestMethod]
    [DataRow("F1", true, true, true, "\x1b[1;8P")]
    [DataRow("F4", false, true, false, "\x1b[1;3S")]
    [DataRow("F12", true, true, true, "\x1b[24;8~")]
    [DataRow("Delete", false, false, true, "\x1b[3;5~")]
    [DataRow("Backspace", false, false, true, "\b")]
    [DataRow("Backspace", false, true, true, "\x1b\b")]
    [DataRow("Backspace", false, true, false, "\x1b\x7f")]
    [DataRow("Tab", true, false, false, "\x1b[Z")]
    [DataRow("Tab", true, true, true, "\x1b\x1b[Z")]
    [DataRow("Enter", false, true, false, "\x1b\r")]
    [DataRow("Escape", false, true, false, "\x1b\x1b")]
    [DataRow("a", false, true, true, "\x1b\x01")]
    [DataRow("Z", true, false, true, "\x1a")]
    [DataRow("@", false, false, true, "\0")]
    [DataRow(" ", false, false, true, "\0")]
    [DataRow("_", false, false, true, "\x1f")]
    [DataRow("?", false, false, true, "?")]
    [DataRow("é", false, true, false, "\x1bé")]
    public void EncodeKey_Modifiers_PreservesWireEncoding(
        string key, bool shift, bool alt, bool ctrl, string expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        using var command = JsonDocument.Parse(JsonSerializer.Serialize(new { key, shift, alt, ctrl }));

        Assert.AreEqual(expected, Hwt1Input.EncodeKey(command.RootElement, terminal));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("hello\r\n世界😀")]
    public void EncodePaste_ModeChanges_PreservesTextAndOnlyAddsRequestedMarkers(string text)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();

        Assert.AreEqual(text, Hwt1Input.EncodePaste(text, terminal));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2004h"));
        Assert.AreEqual("\x1b[200~" + text + "\x1b[201~", Hwt1Input.EncodePaste(text, terminal));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2004l"));
        Assert.AreEqual(text, Hwt1Input.EncodePaste(text, terminal));
    }

    [TestMethod]
    [DataRow("""{"key":"a","ctrl":1}""")]
    [DataRow("""{"key":"a","shift":"true"}""")]
    [DataRow("""{"key":"a","alt":null}""")]
    [DataRow("""{"key":"Unknown"}""")]
    [DataRow("""{"key":""}""")]
    [DataRow("""{"key":"😀"}""")]
    public void EncodeKey_InvalidIntent_RejectsCommand(string json)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        using var command = JsonDocument.Parse(json);

        Assert.ThrowsExactly<InvalidDataException>(() => Hwt1Input.EncodeKey(command.RootElement, terminal));
    }
}
