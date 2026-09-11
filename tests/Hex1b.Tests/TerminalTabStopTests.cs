using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalTabStopTests
{
    [TestMethod]
    [DataRow(80, false)]
    [DataRow(80, true)]
    [DataRow(83, false)]
    [DataRow(83, true)]
    public void Resize_GrowingWidth_ExtendsTabsWithoutWrappingColumnText(int initialWidth, bool reflow)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var headless = new HeadlessPresentationAdapter(initialWidth, 5);
        IHex1bTerminalPresentationAdapter presentation = reflow
            ? headless.WithReflow(AlacrittyReflowStrategy.Instance) : headless;
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(presentation).Build();

        terminal.Resize(154, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;73HMouseTest\t\tRescueDemo\r\nDONE"));

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("MouseTest", snapshot.GetLine(0).Substring(72, 9));
        Assert.AreEqual("RescueDemo", snapshot.GetLine(0).Substring(96, 10));
        Assert.AreEqual(" ", snapshot.GetCell(153, 0).Character);
        Assert.AreEqual("DONE", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(1, terminal.CursorY);
    }

    [TestMethod]
    public void Resize_GrowingWidth_PreservesClearedStopsInExistingColumns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;9H\x1b[g\x1b[1;73H\x1b[g"));

        terminal.Resize(154, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H\tX\x1b[2;65H\tY"));

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("X", snapshot.GetCell(16, 0).Character);
        Assert.AreEqual("Y", snapshot.GetCell(80, 1).Character);
    }

    [TestMethod]
    public void Resize_GrowingAfterClearingAllStops_InitializesOnlyNewColumns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3g"));

        terminal.Resize(154, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\t"));

        Assert.AreEqual(80, terminal.CursorX);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\t"));
        Assert.AreEqual(88, terminal.CursorX);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resize_ShrinkAndRegrow_PreservesOffscreenClearedStops(bool clearAll)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(160, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(clearAll ? "\x1b[3g" : "\x1b[1;97H\x1b[g"));

        terminal.Resize(80, 5);
        terminal.Resize(160, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;89H\tX"));

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("X", snapshot.GetCell(clearAll ? 159 : 104, 0).Character);
        Assert.AreEqual(0, terminal.CursorY);
    }

    [TestMethod]
    public void Resize_GrowingWidth_BackTabUsesNewStopsAndForwardTabStillClamps()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 5).Build();

        terminal.Resize(154, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;154H\x1b[Z"));
        Assert.AreEqual(152, terminal.CursorX);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\t"));
        Assert.AreEqual(153, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
    }
}
