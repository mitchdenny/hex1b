using Hex1b;
using Hex1b.Terminal;

namespace Hex1b.Tests;

public class Osc8HyperlinkTests
{
    [Fact]
    public void ProcessOutput_WithOsc8Sequence_CreatesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 format: ESC ] 8 ; params ; URI ST
        // ST can be ESC \ or BEL (\x07)
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link Text");
        terminal.ProcessOutput("\x1b]8;;\x1b\\"); // End hyperlink
        
        // Should track the hyperlink data
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        Assert.True(terminal.ContainsHyperlinkData());
        
        // The cells with "Link Text" should have the hyperlink data
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.com", hyperlinkData.Uri);
        Assert.Equal("", hyperlinkData.Parameters);
    }

    [Fact]
    public void ProcessOutput_WithOsc8UsingBel_CreatesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 with BEL terminator instead of ESC \
        terminal.ProcessOutput("\x1b]8;;https://example.org\x07");
        terminal.ProcessOutput("Click here");
        terminal.ProcessOutput("\x1b]8;;\x07"); // End hyperlink
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.org", hyperlinkData.Uri);
    }

    [Fact]
    public void ProcessOutput_WithOsc8WithParameters_StoresParameters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // OSC 8 with parameters (e.g., id=unique-id)
        terminal.ProcessOutput("\x1b]8;id=test123;https://example.com/path\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var hyperlinkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(hyperlinkData);
        Assert.Equal("https://example.com/path", hyperlinkData.Uri);
        Assert.Equal("id=test123", hyperlinkData.Parameters);
    }

    [Fact]
    public void ProcessOutput_EndOsc8_ReleasesHyperlink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start hyperlink
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        
        // Verify hyperlink exists
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        var linkData1 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData1);
        
        // End hyperlink
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Write more text without hyperlink
        terminal.ProcessOutput(" Plain");
        
        // The plain text should not have hyperlink
        var linkData2 = terminal.GetHyperlinkDataAt(5, 0);
        Assert.Null(linkData2);
        
        // But the original link text should still have it
        var linkData3 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData3);
    }

    [Fact]
    public void TrackedHyperlink_WhenCellOverwritten_ReleasesReference()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create hyperlink
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        // Overwrite the cells
        terminal.ProcessOutput("\x1b[1;1HXXXXXXXX");
        
        // Hyperlink data should be released (refcount reached 0)
        Assert.Equal(0, terminal.TrackedHyperlinkCount);
    }

    [Fact]
    public void TrackedHyperlink_Deduplication_ReusesSameObject()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create same hyperlink twice
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("First");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        terminal.ProcessOutput(" ");
        
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Second");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Should still only have one unique tracked object
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        
        // Both link texts should reference the same object
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(6, 0);
        Assert.Same(link1, link2);
    }

    [Fact]
    public void TrackedHyperlink_RefCount_IncreasesWithDeduplication()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Create same hyperlink with multiple characters
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Link");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var trackedLink = terminal.GetTrackedHyperlinkAt(0, 0);
        Assert.NotNull(trackedLink);
        
        // RefCount should be 4 (one for each character: L, i, n, k)
        Assert.Equal(4, trackedLink.RefCount);
    }

    [Fact]
    public void TrackedHyperlink_DifferentParameters_CreatesSeparateObjects()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Same URI but different parameters should create different objects
        terminal.ProcessOutput("\x1b]8;id=1;https://example.com\x1b\\");
        terminal.ProcessOutput("A");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        terminal.ProcessOutput("\x1b]8;id=2;https://example.com\x1b\\");
        terminal.ProcessOutput("B");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Should have two unique tracked objects
        Assert.Equal(2, terminal.TrackedHyperlinkCount);
        
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(1, 0);
        Assert.NotSame(link1, link2);
        Assert.Equal("id=1", link1!.Parameters);
        Assert.Equal("id=2", link2!.Parameters);
    }

    [Fact]
    public void ProcessOutput_MultilineHyperlink_TracksAcrossRows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start hyperlink and write across multiple lines
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\");
        terminal.ProcessOutput("Line 1\n");
        terminal.ProcessOutput("Line 2");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // Both lines should have the hyperlink
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        var link2 = terminal.GetHyperlinkDataAt(0, 1);
        
        Assert.NotNull(link1);
        Assert.NotNull(link2);
        Assert.Same(link1, link2);
        Assert.Equal("https://example.com", link1.Uri);
    }

    [Fact]
    public void ProcessOutput_NestedHyperlinks_ReplacesWithNewLink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Start first hyperlink
        terminal.ProcessOutput("\x1b]8;;https://first.com\x1b\\");
        terminal.ProcessOutput("A");
        
        // Start second hyperlink without closing first (should replace)
        terminal.ProcessOutput("\x1b]8;;https://second.com\x1b\\");
        terminal.ProcessOutput("B");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        // First character should have first link
        var link1 = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(link1);
        Assert.Equal("https://first.com", link1.Uri);
        
        // Second character should have second link
        var link2 = terminal.GetHyperlinkDataAt(1, 0);
        Assert.NotNull(link2);
        Assert.Equal("https://second.com", link2.Uri);
        
        // Should have two tracked objects
        Assert.Equal(2, terminal.TrackedHyperlinkCount);
    }

    [Fact]
    public void WorkloadAdapter_WithOsc8_TerminalReceivesHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Write through workload adapter
        workload.Write("\x1b]8;;https://example.com\x1b\\");
        workload.Write("Link");
        workload.Write("\x1b]8;;\x1b\\");
        
        // Flush should process it
        terminal.FlushOutput();
        
        Assert.Equal(1, terminal.TrackedHyperlinkCount);
        Assert.True(terminal.ContainsHyperlinkData());
        
        var linkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData);
        Assert.Equal("https://example.com", linkData.Uri);
    }

    [Fact]
    public void ProcessOutput_ComplexUri_PreservesAllCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Complex URI with query parameters, hash, etc.
        var uri = "https://example.com/path?foo=bar&baz=qux#section";
        terminal.ProcessOutput($"\x1b]8;;{uri}\x1b\\");
        terminal.ProcessOutput("X");
        terminal.ProcessOutput("\x1b]8;;\x1b\\");
        
        var linkData = terminal.GetHyperlinkDataAt(0, 0);
        Assert.NotNull(linkData);
        Assert.Equal(uri, linkData.Uri);
    }
}
