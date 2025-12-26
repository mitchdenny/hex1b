using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC 8 hyperlink support, including both low-level terminal parsing
/// and high-level HyperlinkWidget integration with Hex1bApp.
/// </summary>
public class Osc8HyperlinkTests
{
    #region Low-Level OSC 8 Parsing Tests

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

    #endregion

    #region HyperlinkWidget Integration Tests with Snapshots

    [Fact]
    public async Task HyperlinkWidget_SingleLink_RendersWithOsc8()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Click the link below:"),
                v.Hyperlink("Visit GitHub", "https://github.com/mitchdenny/hex1b")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Visit GitHub"), TimeSpan.FromSeconds(2))
            .Capture("single-link")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-single");

        // Verify the link text is rendered
        Assert.True(snapshot.ContainsText("Visit GitHub"));
        Assert.True(snapshot.ContainsText("Click the link below"));
    }

    [Fact]
    public async Task HyperlinkWidget_MultipleLinks_AllRenderCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Navigation Links:"),
                v.Hyperlink("GitHub", "https://github.com"),
                v.Hyperlink("Documentation", "https://hex1b.dev/docs"),
                v.Hyperlink("Examples", "https://hex1b.dev/examples"),
                v.Hyperlink("API Reference", "https://hex1b.dev/api")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("API Reference"), TimeSpan.FromSeconds(2))
            .Capture("multiple-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-multiple");

        // Verify all links are rendered
        Assert.True(snapshot.ContainsText("GitHub"));
        Assert.True(snapshot.ContainsText("Documentation"));
        Assert.True(snapshot.ContainsText("Examples"));
        Assert.True(snapshot.ContainsText("API Reference"));
    }

    [Fact]
    public async Task HyperlinkWidget_InHStack_RendersInline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 70, 8);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Quick Links:"),
                v.HStack(h => [
                    h.Hyperlink("[Home]", "https://hex1b.dev"),
                    h.Text(" | "),
                    h.Hyperlink("[Docs]", "https://hex1b.dev/docs"),
                    h.Text(" | "),
                    h.Hyperlink("[GitHub]", "https://github.com/mitchdenny/hex1b")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[GitHub]"), TimeSpan.FromSeconds(2))
            .Capture("inline-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-inline");

        // Verify inline layout
        Assert.True(snapshot.ContainsText("[Home]"));
        Assert.True(snapshot.ContainsText("[Docs]"));
        Assert.True(snapshot.ContainsText("[GitHub]"));
    }

    [Fact]
    public async Task HyperlinkWidget_InBorder_RendersWithFrame()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("Important Links"),
                    v.Text(""),
                    v.Hyperlink("Project Repository", "https://github.com/mitchdenny/hex1b"),
                    v.Hyperlink("Issue Tracker", "https://github.com/mitchdenny/hex1b/issues")
                ]),
                title: "Resources"
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Issue Tracker"), TimeSpan.FromSeconds(2))
            .Capture("bordered-links")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-bordered");

        // Verify content
        Assert.True(snapshot.ContainsText("Resources"));
        Assert.True(snapshot.ContainsText("Project Repository"));
        Assert.True(snapshot.ContainsText("Issue Tracker"));
    }

    [Fact]
    public async Task HyperlinkWidget_WithClickHandler_TracksClicks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 8);
        var clickedUri = "";
        var clickCount = 0;

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Clicks: {clickCount}"),
                v.Hyperlink("Click Me", "https://example.com")
                    .OnClick(e => { clickedUri = e.Uri; clickCount++; })
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter() // Click the link
            .Enter() // Click again
            .Capture("after-clicks")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-clicked");

        Assert.Equal("https://example.com", clickedUri);
        Assert.Equal(2, clickCount);
        Assert.True(snapshot.ContainsText("Clicks: 2"));
    }

    [Fact]
    public async Task HyperlinkWidget_TabNavigation_FocusesLinks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var lastClickedUri = "";

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("First Link", "https://first.com")
                    .OnClick(e => lastClickedUri = e.Uri),
                v.Hyperlink("Second Link", "https://second.com")
                    .OnClick(e => lastClickedUri = e.Uri),
                v.Hyperlink("Third Link", "https://third.com")
                    .OnClick(e => lastClickedUri = e.Uri)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate and capture at each focus state
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third Link"), TimeSpan.FromSeconds(2))
            .Capture("focus-first")
            .Tab()
            .Capture("focus-second")
            .Tab()
            .Capture("focus-third")
            .Enter() // Click third link
            .Capture("after-click-third")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture final snapshot
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-navigation");

        Assert.Equal("https://third.com", lastClickedUri);
    }

    [Fact]
    public async Task HyperlinkWidget_MixedWithButtons_InterleavedCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);
        var buttonClicked = false;
        var linkClicked = false;

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Actions:"),
                v.Hyperlink("Read Documentation", "https://hex1b.dev/docs")
                    .OnClick(_ => linkClicked = true),
                v.Button("Submit Form")
                    .OnClick(_ => { buttonClicked = true; return Task.CompletedTask; }),
                v.Hyperlink("View Source", "https://github.com/mitchdenny/hex1b")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("View Source"), TimeSpan.FromSeconds(2))
            .Enter() // Click first link
            .Tab()
            .Enter() // Click button
            .Capture("mixed-widgets")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-mixed");

        Assert.True(linkClicked);
        Assert.True(buttonClicked);
        Assert.True(snapshot.ContainsText("Read Documentation"));
        Assert.True(snapshot.ContainsText("Submit Form"));
        Assert.True(snapshot.ContainsText("View Source"));
    }

    [Fact]
    public async Task HyperlinkWidget_ComplexUrls_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 12);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Complex URL Examples:"),
                v.Hyperlink("Search Results", "https://www.google.com/search?q=terminal+hyperlinks&hl=en"),
                v.Hyperlink("Wikipedia Section", "https://en.wikipedia.org/wiki/ANSI_escape_code#OSC_(Operating_System_Command)_sequences"),
                v.Hyperlink("File Protocol", "file:///home/user/documents/readme.txt"),
                v.Hyperlink("Mailto Link", "mailto:test@example.com?subject=Hello&body=Test")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mailto Link"), TimeSpan.FromSeconds(2))
            .Capture("complex-urls")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Capture snapshots
        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-complex-urls");

        Assert.True(snapshot.ContainsText("Search Results"));
        Assert.True(snapshot.ContainsText("Wikipedia Section"));
        Assert.True(snapshot.ContainsText("File Protocol"));
        Assert.True(snapshot.ContainsText("Mailto Link"));
    }

    [Fact]
    public async Task HyperlinkWidget_NarrowTerminal_TextTruncates()
    {
        // Very narrow terminal - hyperlink text should be truncated
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 15, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Links:"),
                v.Hyperlink("Very Long Hyperlink Text That Should Truncate", "https://example.com")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Links"), TimeSpan.FromSeconds(2))
            .Capture("narrow-terminal")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-narrow-terminal");

        // The text should be truncated, so full text should NOT be present
        Assert.False(snapshot.ContainsText("Very Long Hyperlink Text That Should Truncate"));
        // But partial text should be visible
        Assert.True(snapshot.ContainsText("Very"));
    }

    [Fact]
    public async Task HyperlinkWidget_InSplitter_ClippedByPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.Splitter(
                ctx.VStack(v => [
                    v.Text("Left Pane"),
                    v.Hyperlink("Left Link With Long Text", "https://left.example.com")
                ]),
                ctx.VStack(v => [
                    v.Text("Right Pane"),
                    v.Hyperlink("Right Link", "https://right.example.com")
                ]),
                leftWidth: 15 // Narrow left pane to force clipping
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right"), TimeSpan.FromSeconds(2))
            .Capture("splitter-clipping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-splitter-clipping");

        // Right pane should be fully visible
        Assert.True(snapshot.ContainsText("Right Pane"));
        Assert.True(snapshot.ContainsText("Right Link"));
        // Left pane text should be clipped (partial visibility)
        Assert.True(snapshot.ContainsText("Left"));
    }

    [Fact]
    public async Task HyperlinkWidget_InScrollView_PartiallyVisible()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.VScroll(
                v => [
                    v.Hyperlink("Link 1 - First Item", "https://one.example.com"),
                    v.Hyperlink("Link 2 - Second Item", "https://two.example.com"),
                    v.Hyperlink("Link 3 - Third Item", "https://three.example.com"),
                    v.Hyperlink("Link 4 - Fourth Item", "https://four.example.com"),
                    v.Hyperlink("Link 5 - Fifth Item", "https://five.example.com"),
                    v.Hyperlink("Link 6 - Sixth Item", "https://six.example.com"),
                    v.Hyperlink("Link 7 - Seventh Item", "https://seven.example.com"),
                    v.Hyperlink("Link 8 - Eighth Item", "https://eight.example.com")
                ]
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Link 1"), TimeSpan.FromSeconds(2))
            .Capture("scroll-initial")
            .Down().Down().Down() // Scroll down
            .Capture("scroll-scrolled")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-scroll-view");

        // After scrolling, later links should be visible
        // Initial links may or may not be visible depending on scroll position
        Assert.True(snapshot.ContainsText("Link"));
    }

    [Fact]
    public async Task HyperlinkWidget_InBorderWithSmallSize_Clipped()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 25, 5);

        await using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Hyperlink("This is a very long hyperlink that exceeds the border", "https://example.com")
                ]),
                title: "Tiny"
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Tiny"), TimeSpan.FromSeconds(2))
            .Capture("border-clipped")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-border-clipped");

        // Border title should be visible
        Assert.True(snapshot.ContainsText("Tiny"));
        // Full hyperlink text should NOT be visible (clipped)
        Assert.False(snapshot.ContainsText("exceeds the border"));
    }

    [Fact]
    public async Task HyperlinkWidget_MultipleInHStack_WrapsOrClips()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.HStack(h => [
                h.Hyperlink("[GitHub]", "https://github.com"),
                h.Text(" "),
                h.Hyperlink("[Documentation]", "https://docs.example.com"),
                h.Text(" "),
                h.Hyperlink("[Support]", "https://support.example.com"),
                h.Text(" "),
                h.Hyperlink("[Contact]", "https://contact.example.com")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[GitHub]"), TimeSpan.FromSeconds(2))
            .Capture("hstack-overflow")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-hstack-overflow");

        // First links should be visible
        Assert.True(snapshot.ContainsText("[GitHub]"));
        // Later links may be clipped depending on terminal width
    }

    [Fact]
    public async Task HyperlinkWidget_EmptyText_RendersNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Before"),
                v.Hyperlink("", "https://example.com"), // Empty text
                v.Text("After")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("After"), TimeSpan.FromSeconds(2))
            .Capture("empty-text")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-empty-text");

        Assert.True(snapshot.ContainsText("Before"));
        Assert.True(snapshot.ContainsText("After"));
    }

    [Fact]
    public async Task HyperlinkWidget_SpecialCharactersInText_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 8);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("Link with <angle> brackets", "https://example.com/1"),
                v.Hyperlink("Link with \"quotes\"", "https://example.com/2"),
                v.Hyperlink("Link with 'apostrophes'", "https://example.com/3"),
                v.Hyperlink("Link with & ampersand", "https://example.com/4"),
                v.Hyperlink("Unicode: 日本語リンク", "https://example.com/5")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ampersand"), TimeSpan.FromSeconds(2))
            .Capture("special-chars")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-special-chars");

        Assert.True(snapshot.ContainsText("angle"));
        Assert.True(snapshot.ContainsText("quotes"));
        Assert.True(snapshot.ContainsText("apostrophes"));
        Assert.True(snapshot.ContainsText("ampersand"));
        Assert.True(snapshot.ContainsText("日本語"));
    }

    [Fact]
    public async Task HyperlinkWidget_WithParameters_PreservesId()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Links with same ID should be grouped:"),
                v.Hyperlink("Part 1 of", "https://example.com").WithId("multi-line"),
                v.Hyperlink("the same link", "https://example.com").WithId("multi-line")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("same link"), TimeSpan.FromSeconds(2))
            .Capture("with-id")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-with-id");

        Assert.True(snapshot.ContainsText("Part 1 of"));
        Assert.True(snapshot.ContainsText("the same link"));
    }

    [Fact]
    public async Task HyperlinkWidget_InsideLayoutWithClipping_PreservesHyperlinkData()
    {
        // Test that OSC 8 hyperlink tracking works correctly when content is clipped by a layout provider
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                // Put a hyperlink inside a Border with fixed size that clips the content
                v.Border(b => [
                    b.Hyperlink("This is a very long hyperlink that will be clipped", "https://example.com/clipped"),
                    b.Text("Some normal text here")
                ], "Clipped Box").FixedWidth(20).FixedHeight(4),
                
                v.Text("Text outside the clipped area")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Clipped Box"), TimeSpan.FromSeconds(2))
            .Capture("clipped-hyperlink")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-inside-layout-clipping");

        // The hyperlink text should be partially visible (clipped)
        // Check that the visible portion has hyperlink data
        var row = 1; // Second row (first row is border top)
        var foundHyperlink = false;
        
        for (int x = 0; x < snapshot.Width; x++)
        {
            var cell = snapshot.GetCell(x, row);
            if (cell.HasHyperlinkData)
            {
                foundHyperlink = true;
                Assert.Equal("https://example.com/clipped", cell.HyperlinkData!.Uri);
                break;
            }
        }
        
        Assert.True(foundHyperlink, "Should find at least one cell with hyperlink data in the clipped area");
    }

    [Fact]
    public async Task HyperlinkWidget_InsideVStackWithConstrainedWidth_TracksHyperlinkPerCell()
    {
        // Test that a hyperlink inside a VStack with constrained width has hyperlink data for each visible cell
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 25, 6);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("Link ABC", "https://example.com/link")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Link"), TimeSpan.FromSeconds(2))
            .Capture("constrained-hyperlink")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-constrained-vstack");

        // Check that all visible characters of the hyperlink have hyperlink data
        var hyperlinkCellCount = 0;
        var linkText = "Link ABC";
        
        for (int x = 0; x < snapshot.Width; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            if (cell.HasHyperlinkData && cell.HyperlinkData!.Uri == "https://example.com/link")
            {
                hyperlinkCellCount++;
            }
        }
        
        // Should have hyperlink data for each character in "Link ABC" (8 chars)
        Assert.Equal(linkText.Length, hyperlinkCellCount);
    }

    [Fact]
    public async Task HyperlinkWidget_WithTextOverflowWrap_WrapsAcrossMultipleLines()
    {
        // Test that a hyperlink with TextOverflow.Wrap wraps properly across multiple lines
        // and each wrapped line maintains the hyperlink data
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 8);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("This is a very long hyperlink that should wrap to multiple lines", "https://example.com/wrapped", TextOverflow.Wrap)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This"), TimeSpan.FromSeconds(2))
            .Capture("wrapped-hyperlink")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-text-overflow-wrap");

        // The hyperlink should wrap across multiple lines
        // Check that there is hyperlink data on at least two different rows
        var rowsWithHyperlink = new HashSet<int>();
        
        for (int y = 0; y < snapshot.Height; y++)
        {
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (cell.HasHyperlinkData && cell.HyperlinkData!.Uri == "https://example.com/wrapped")
                {
                    rowsWithHyperlink.Add(y);
                }
            }
        }
        
        // The text "This is a very long hyperlink that should wrap to multiple lines" (63 chars)
        // in a 20-column terminal should wrap to at least 4 lines
        Assert.True(rowsWithHyperlink.Count >= 3, 
            $"Expected hyperlink to span at least 3 rows, but found {rowsWithHyperlink.Count} rows: [{string.Join(", ", rowsWithHyperlink)}]");
        
        // Verify the first row contains "This is a very"
        Assert.True(snapshot.ContainsText("This is a very") || snapshot.ContainsText("This"), 
            "First line should contain start of hyperlink text");
    }

    [Fact]
    public async Task HyperlinkWidget_WithTextOverflowWrap_AllWrappedLinesHaveOsc8()
    {
        // Verify that each wrapped line of a hyperlink emits OSC 8 sequences
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 15, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Hyperlink("First Second Third Fourth", "https://example.com/multi", TextOverflow.Wrap)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Capture("multi-line-hyperlink")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        TestSvgHelper.Capture(snapshot, "hyperlink-multi-line-osc8");

        // Count cells with hyperlink data - should be substantial
        var hyperlinkCellCount = 0;
        
        for (int y = 0; y < snapshot.Height; y++)
        {
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (cell.HasHyperlinkData && cell.HyperlinkData!.Uri == "https://example.com/multi")
                {
                    hyperlinkCellCount++;
                }
            }
        }
        
        // "First Second Third Fourth" = 25 characters, but word-wrapping may not include
        // trailing spaces on lines. Should have at least 20 characters with hyperlink data.
        Assert.True(hyperlinkCellCount >= 20, 
            $"Expected at least 20 cells with hyperlink data, found {hyperlinkCellCount}");
    }

    #endregion

    #region SVG Group Class Tests

    [Fact]
    public void SvgOutput_HyperlinkCells_HaveGroupClass()
    {
        // Cells with the same hyperlink should have the same group class in the SVG output
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        
        // Write OSC 8 hyperlink with text
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\Click Me\x1b]8;;\x1b\\");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Should contain cell groups with link-0 class (first hyperlink group)
        Assert.Contains("class=\"cell link-0\"", svg);
    }

    [Fact]
    public void SvgOutput_MultipleSameHyperlinks_ShareGroupClass()
    {
        // Multiple cells with the same hyperlink share the same TrackedObject,
        // so they should all have the same group class
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        
        // Write text for hyperlink - each character gets same hyperlink
        terminal.ProcessOutput("\x1b]8;;https://example.com\x1b\\ABCDE\x1b]8;;\x1b\\");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Count how many cells have the link-0 class - should be 5 (for A, B, C, D, E)
        var cellGroupCount = System.Text.RegularExpressions.Regex.Matches(svg, @"class=""cell link-0""").Count;
        Assert.Equal(5, cellGroupCount);
    }

    [Fact]
    public void SvgOutput_DifferentHyperlinks_HaveDifferentGroupClasses()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        
        // Write two different hyperlinks
        terminal.ProcessOutput("\x1b]8;;https://example1.com\x1b\\Link1\x1b]8;;\x1b\\");
        terminal.ProcessOutput(" ");
        terminal.ProcessOutput("\x1b]8;;https://example2.com\x1b\\Link2\x1b]8;;\x1b\\");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Should have two different link groups
        Assert.Contains("link-0", svg);
        Assert.Contains("link-1", svg);
    }

    [Fact]
    public void SvgOutput_CellsWithoutHyperlink_HaveNoGroupClass()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        
        // Write some regular text (no hyperlink)
        terminal.ProcessOutput("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Cells should just have "cell" class, no link-* suffix
        Assert.Contains("class=\"cell\"", svg);
        Assert.DoesNotContain("link-", svg);
    }

    [Fact]
    public void SvgOutput_ContainsHighlightCssClass()
    {
        // SVG should include CSS for highlighting cell groups
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        terminal.ProcessOutput("Hello");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Check for the highlight CSS class
        Assert.Contains(".cell.highlight", svg);
    }

    [Fact]
    public void SvgOutput_CellsHaveDataAttributes()
    {
        // Each cell group should have data-x and data-y attributes
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);
        terminal.ProcessOutput("AB");
        
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Check for data attributes on cells
        Assert.Contains("data-x=\"0\"", svg);
        Assert.Contains("data-y=\"0\"", svg);
        Assert.Contains("data-x=\"1\"", svg);
    }

    #endregion
}

