using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Diagnostic tests for embedded terminal resize behavior.
/// These tests reproduce the EmbeddedTerminalDemo pattern and verify that
/// terminals properly fill their enclosing borders at the correct size.
/// </summary>
/// <remarks>
/// <para>
/// These tests create a larger terminal (150x40) to clearly observe resize issues.
/// They use asciinema recording and snapshot captures to diagnose problems.
/// </para>
/// <para>
/// Issue being diagnosed: New terminals may not fill the enclosing border,
/// possibly due to timing issues with resize propagation.
/// </para>
/// </remarks>
public class EmbeddedTerminalResizeDiagnosticTests
{
    /// <summary>
    /// Diagnostic test 1: Open a terminal via Alt+F menu and verify bash prompt appears.
    /// Uses 150x40 terminal size with asciinema recording.
    /// </summary>
    [Fact]
    public async Task Diagnostic_OpenTerminalViaMenu_BashPromptAppears()
    {
        // Arrange - Create temp file for recording
        var tempFile = Path.Combine(Path.GetTempPath(), $"terminal-resize-diag1-{Guid.NewGuid()}.cast");
        
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 150,
            Height = 40,
            WorkloadAdapter = workload
        };
        
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Diagnostic: Open Terminal via Menu",
            IdleTimeLimit = 2.0f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // State mirroring EmbeddedTerminalDemo
        var terminals = new List<TerminalSession>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        Hex1bApp? appRef = null;
        
        void AddTerminal()
        {
            var id = nextTerminalId++;
            // Use dimensions that should fill the available space
            var childTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(148, 36) // Approximate inner size
                .WithPtyProcess("bash", "--norc")
                .WithTerminalWidget(out var handle)
                .Build();
            
            var session = new TerminalSession(id, childTerminal, handle);
            
            lock (terminalLock)
            {
                terminals.Add(session);
                activeTerminalId = id;
            }
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await childTerminal.RunAsync(TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException) { }
            });
            
            appRef?.RequestFocus(node => node is TerminalNode tn && tn.Handle == handle);
            appRef?.Invalidate();
        }
        
        Hex1bWidget BuildUI(RootContext ctx)
        {
            List<TerminalSession> currentTerminals;
            lock (terminalLock)
            {
                currentTerminals = [.. terminals];
            }
            
            Hex1bWidget BuildMainContent<TParent>(WidgetContext<TParent> v) where TParent : Hex1bWidget
            {
                if (currentTerminals.Count == 0)
                {
                    return v.Align(Alignment.Center,
                        v.Border(
                            v.Text("Use File → New Terminal"),
                            title: "Welcome"
                        )
                    ).Fill();
                }
                
                var activeSession = currentTerminals.FirstOrDefault(s => s.Id == activeTerminalId) 
                                 ?? currentTerminals[0];
                
                return v.Border(
                    v.Terminal(activeSession.Handle).Fill(),
                    title: $"Terminal {activeSession.Id}"
                ).Fill();
            }
            
            return ctx.VStack(v =>
            [
                v.MenuBar(m =>
                [
                    m.Menu("File", m =>
                    [
                        m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                        m.Separator(),
                        m.MenuItem("Quit").OnActivated(_ => appRef?.RequestStop())
                    ]),
                    m.Menu("Help", m =>
                    [
                        m.MenuItem("About").OnActivated(_ => { })
                    ])
                ]),
                BuildMainContent(v),
                v.InfoBar(["Ctrl+N", "New Terminal"])
            ]).WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.N).Global().Action(_ => AddTerminal(), "Add terminal");
                bindings.Ctrl().Key(Hex1bKey.Q).Global().Action(_ => appRef?.RequestStop(), "Quit");
            });
        }
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildUI(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        appRef = app;
        
        // Act - Run the sequence
        recorder.AddMarker("Start");
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("File") && s.ContainsText("Help"), TimeSpan.FromSeconds(3), "menu bar to render")
                .Capture("01-initial")
                // Use Enter to open menu (focus starts on File in menu bar), then Enter again to activate first item
                .Enter() // Open File menu (focus is on "File" in menu bar)
                .WaitUntil(s => s.ContainsText("New Terminal"), TimeSpan.FromSeconds(2), "File menu to open")
                .Capture("02-menu-open")
                .Enter() // Activate first menu item (New Terminal)
                .WaitUntil(s => s.ContainsText("Terminal 1"), TimeSpan.FromSeconds(5), "terminal to be created")
                .Capture("03-terminal-created")
                .Wait(1500) // Wait for bash to start and show prompt
                .WaitUntil(s => s.ContainsText("$") || s.ContainsText("bash") || s.ContainsText("#"), TimeSpan.FromSeconds(5), "bash prompt to appear")
                .Capture("04-bash-prompt")
                .Ctrl().Key(Hex1bKey.C) // Exit
                .Build()
                .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Capture final state regardless of outcome
            var finalSnapshot = terminal.CreateSnapshot();
            TestCaptureHelper.Capture(finalSnapshot, "diag1-final");
            await TestCaptureHelper.CaptureCastAsync(recorder, "diag1-recording", TestContext.Current.CancellationToken);
            
            // Clean up child terminals
            foreach (var session in terminals)
            {
                await session.Terminal.DisposeAsync();
            }
        }
        
        await runTask;
        
        // Assert - Should have seen bash prompt
        var screen = terminal.GetScreenText();
        Assert.True(
            screen.Contains("$") || screen.Contains("bash") || screen.Contains("#"),
            $"Expected bash prompt to appear. Screen content:\n{screen[..Math.Min(screen.Length, 1000)]}"
        );
    }
    
    /// <summary>
    /// Diagnostic test 2: Open two terminals and run stty size on the second one.
    /// This tests whether the second terminal gets the correct size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KNOWN BUG: The second terminal is created but shows no content.
    /// The border renders correctly with "Terminal 2" title, but the
    /// terminal content area is completely empty (no bash prompt, no output).
    /// </para>
    /// <para>
    /// This indicates a resize/initialization issue where new terminals
    /// are not properly filling their enclosing border or receiving 
    /// PTY content after creation.
    /// </para>
    /// <para>
    /// FIXED: The issue was that TerminalNode.Arrange() only resized the handle
    /// when bounds changed. When switching terminals (same node, different handle),
    /// bounds stayed the same so resize was skipped. The fix tracks handle changes
    /// separately and resizes on handle swap.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Diagnostic_SecondTerminal_SttySize_ReportsCorrectDimensions()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"terminal-resize-diag2-{Guid.NewGuid()}.cast");
        
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 150,
            Height = 40,
            WorkloadAdapter = workload
        };
        
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Diagnostic: Second Terminal stty size",
            IdleTimeLimit = 2.0f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // State
        var terminals = new List<TerminalSession>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        Hex1bApp? appRef = null;
        
        // Expected size for inner terminal: 150-2 (border) x 40-4 (menu+info+borders) = 148x36
        // But the actual size should be 148x36 or close to it
        var expectedWidth = 148;
        var expectedHeight = 36;
        
        void AddTerminal()
        {
            var id = nextTerminalId++;
            var childTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(expectedWidth, expectedHeight)
                .WithPtyProcess("bash", "--norc")
                .WithTerminalWidget(out var handle)
                .Build();
            
            var session = new TerminalSession(id, childTerminal, handle);
            
            lock (terminalLock)
            {
                terminals.Add(session);
                activeTerminalId = id;
            }
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await childTerminal.RunAsync(TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException) { }
            });
            
            appRef?.RequestFocus(node => node is TerminalNode tn && tn.Handle == handle);
            appRef?.Invalidate();
        }
        
        Hex1bWidget BuildUI(RootContext ctx)
        {
            List<TerminalSession> currentTerminals;
            lock (terminalLock)
            {
                currentTerminals = [.. terminals];
            }
            
            Hex1bWidget BuildMainContent<TParent>(WidgetContext<TParent> v) where TParent : Hex1bWidget
            {
                if (currentTerminals.Count == 0)
                {
                    return v.Align(Alignment.Center,
                        v.Border(
                            v.Text("Use File → New Terminal"),
                            title: "Welcome"
                        )
                    ).Fill();
                }
                
                var activeSession = currentTerminals.FirstOrDefault(s => s.Id == activeTerminalId) 
                                 ?? currentTerminals[0];
                
                return v.Border(
                    v.Terminal(activeSession.Handle).Fill(),
                    title: $"Terminal {activeSession.Id}"
                ).Fill();
            }
            
            return ctx.VStack(v =>
            [
                v.MenuBar(m =>
                [
                    m.Menu("File", m =>
                    [
                        m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                        m.Separator(),
                        m.MenuItem("Quit").OnActivated(_ => appRef?.RequestStop())
                    ]),
                    m.Menu("Help", m =>
                    [
                        m.MenuItem("About").OnActivated(_ => { })
                    ])
                ]),
                BuildMainContent(v),
                v.InfoBar(["Ctrl+N", "New Terminal"])
            ]).WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.N).Global().Action(_ => AddTerminal(), "Add terminal");
                bindings.Ctrl().Key(Hex1bKey.Q).Global().Action(_ => appRef?.RequestStop(), "Quit");
            });
        }
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildUI(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        appRef = app;
        
        // Act
        recorder.AddMarker("Start");
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        string? sttyOutput = null;
        
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                // Wait for initial UI
                .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(3), "menu bar")
                .Capture("01-initial")
                
                // Open first terminal (use Enter twice: open menu, activate item)
                .Enter() // Open File menu
                .WaitUntil(s => s.ContainsText("New Terminal"), TimeSpan.FromSeconds(2), "File menu")
                .Capture("02-first-menu")
                .Enter() // Activate New Terminal
                .WaitUntil(s => s.ContainsText("Terminal 1"), TimeSpan.FromSeconds(5), "first terminal")
                .Capture("03-first-terminal")
                .Wait(1500) // Wait for bash
                .WaitUntil(s => s.ContainsText("$") || s.ContainsText("#"), TimeSpan.FromSeconds(5), "first prompt")
                .Capture("04-first-prompt")
                
                // Open second terminal using Ctrl+N (focus is on first terminal, so Enter won't work)
                .Ctrl().Key(Hex1bKey.N) // Create second terminal via global shortcut
                .WaitUntil(s => s.ContainsText("Terminal 2"), TimeSpan.FromSeconds(5), "second terminal")
                .Capture("05-second-terminal")
                .Wait(3000) // Wait longer for bash to start in second terminal
                .Capture("06-after-wait")
                .WaitUntil(s => s.ContainsText("$") || s.ContainsText("#") || s.ContainsText("bash"), TimeSpan.FromSeconds(10), "second prompt")
                .Capture("07-second-prompt")
                
                // Run stty size
                .Type("stty size")
                .Enter()
                .Wait(500) // Wait for output
                .Capture("08-stty-output")
                
                // Exit
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
            // Capture the stty output
            sttyOutput = terminal.GetScreenText();
        }
        finally
        {
            // Capture final state
            var finalSnapshot = terminal.CreateSnapshot();
            TestCaptureHelper.Capture(finalSnapshot, "diag2-final");
            await TestCaptureHelper.CaptureCastAsync(recorder, "diag2-recording", TestContext.Current.CancellationToken);
            
            // Clean up
            foreach (var session in terminals)
            {
                await session.Terminal.DisposeAsync();
            }
        }
        
        await runTask;
        
        // Assert - Parse and check stty size output
        // stty size outputs: "rows cols\n" e.g., "36 148"
        Assert.NotNull(sttyOutput);
        
        // Log the screen for debugging
        TestContext.Current.AddAttachment("stty-output.txt", sttyOutput);
        
        // Try to find the stty size output (format: "rows cols")
        // It should appear after "stty size" command
        var lines = sttyOutput.Split('\n');
        string? sttySizeLine = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("stty size") && i + 1 < lines.Length)
            {
                // The next line should be the output
                sttySizeLine = lines[i + 1].Trim();
                break;
            }
        }
        
        // Also search for any line that looks like "rows cols" format
        if (string.IsNullOrEmpty(sttySizeLine))
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && 
                    int.TryParse(parts[0], out _) && 
                    int.TryParse(parts[1], out _))
                {
                    sttySizeLine = trimmed;
                    break;
                }
            }
        }
        
        Assert.False(string.IsNullOrEmpty(sttySizeLine), 
            $"Could not find stty size output. Screen:\n{sttyOutput[..Math.Min(sttyOutput.Length, 2000)]}");
        
        var sizeParts = sttySizeLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(sizeParts.Length >= 2, $"Invalid stty size output format: '{sttySizeLine}'");
        
        var rows = int.Parse(sizeParts[0]);
        var cols = int.Parse(sizeParts[1]);
        
        // Log actual vs expected
        TestContext.Current.SendDiagnosticMessage($"stty size output: {rows} rows x {cols} cols");
        TestContext.Current.SendDiagnosticMessage($"Expected size: {expectedHeight} rows x {expectedWidth} cols");
        
        // Verify the size is close to expected (allow some variance for border calculations)
        // The issue we're diagnosing is that the second terminal might be much smaller (e.g., 40x24)
        Assert.True(cols >= expectedWidth - 5, 
            $"Terminal width too small! Got {cols}, expected at least {expectedWidth - 5}. This indicates resize issue.");
        Assert.True(rows >= expectedHeight - 5, 
            $"Terminal height too small! Got {rows}, expected at least {expectedHeight - 5}. This indicates resize issue.");
    }
    
    /// <summary>
    /// Diagnostic test 3: Minimal reproduction of the second terminal empty content bug.
    /// Opens two terminals and checks if the second one shows any bash prompt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BUG REPRODUCTION: When creating a second terminal via Ctrl+N:
    /// - The border renders correctly with "Terminal 2" title
    /// - But the terminal content area is completely empty
    /// - No bash prompt, no output, nothing inside the border
    /// </para>
    /// <para>
    /// Expected behavior: Second terminal should show bash prompt like the first one.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Diagnostic_SecondTerminal_ContentIsEmpty_BugRepro()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"terminal-resize-diag3-{Guid.NewGuid()}.cast");
        
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 150,
            Height = 40,
            WorkloadAdapter = workload
        };
        
        var recorder = new AsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "BUG REPRO: Second Terminal Empty Content",
            IdleTimeLimit = 2.0f
        });
        terminalOptions.WorkloadFilters.Add(recorder);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var terminals = new List<TerminalSession>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        Hex1bApp? appRef = null;
        
        void AddTerminal()
        {
            var id = nextTerminalId++;
            var childTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(148, 36)
                .WithPtyProcess("bash", "--norc")
                .WithTerminalWidget(out var handle)
                .Build();
            
            var session = new TerminalSession(id, childTerminal, handle);
            
            lock (terminalLock)
            {
                terminals.Add(session);
                activeTerminalId = id;
            }
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await childTerminal.RunAsync(TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException) { }
            });
            
            appRef?.RequestFocus(node => node is TerminalNode tn && tn.Handle == handle);
            appRef?.Invalidate();
        }
        
        Hex1bWidget BuildUI(RootContext ctx)
        {
            List<TerminalSession> currentTerminals;
            lock (terminalLock)
            {
                currentTerminals = [.. terminals];
            }
            
            Hex1bWidget BuildMainContent<TParent>(WidgetContext<TParent> v) where TParent : Hex1bWidget
            {
                if (currentTerminals.Count == 0)
                {
                    return v.Align(Alignment.Center,
                        v.Border(
                            v.Text("Use File → New Terminal"),
                            title: "Welcome"
                        )
                    ).Fill();
                }
                
                var activeSession = currentTerminals.FirstOrDefault(s => s.Id == activeTerminalId) 
                                 ?? currentTerminals[0];
                
                return v.Border(
                    v.Terminal(activeSession.Handle).Fill(),
                    title: $"Terminal {activeSession.Id}"
                ).Fill();
            }
            
            return ctx.VStack(v =>
            [
                v.MenuBar(m =>
                [
                    m.Menu("File", m =>
                    [
                        m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                        m.Separator(),
                        m.MenuItem("Quit").OnActivated(_ => appRef?.RequestStop())
                    ]),
                    m.Menu("Help", m =>
                    [
                        m.MenuItem("About").OnActivated(_ => { })
                    ])
                ]),
                BuildMainContent(v),
                v.InfoBar(["Ctrl+N", "New Terminal"])
            ]).WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.N).Global().Action(_ => AddTerminal(), "Add terminal");
                bindings.Ctrl().Key(Hex1bKey.Q).Global().Action(_ => appRef?.RequestStop(), "Quit");
            });
        }
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildUI(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        appRef = app;
        
        // Act
        recorder.AddMarker("Start");
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        bool firstTerminalHasPrompt = false;
        bool secondTerminalHasPrompt = false;
        string firstTerminalScreen = "";
        string secondTerminalScreen = "";
        
        try
        {
            // Open first terminal using Ctrl+N (global shortcut, simpler than menu navigation)
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(3), "menu bar")
                .Ctrl().Key(Hex1bKey.N) // Use global shortcut for first terminal too
                .WaitUntil(s => s.ContainsText("Terminal 1"), TimeSpan.FromSeconds(5), "first terminal")
                .Wait(2000)
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            
            // Check first terminal
            firstTerminalScreen = terminal.GetScreenText();
            firstTerminalHasPrompt = firstTerminalScreen.Contains("$") || 
                                     firstTerminalScreen.Contains("#") ||
                                     firstTerminalScreen.Contains("bash");
            
            TestCaptureHelper.Capture(terminal.CreateSnapshot(), "diag3-first-terminal");
            TestContext.Current.SendDiagnosticMessage($"First terminal has prompt: {firstTerminalHasPrompt}");
            
            // Open second terminal
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.N) // Global shortcut
                .WaitUntil(s => s.ContainsText("Terminal 2"), TimeSpan.FromSeconds(5), "second terminal title")
                .Wait(3000) // Wait for bash to start
                .Capture("diag3-after-wait")
                .Build()
                .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
            // Check second terminal
            secondTerminalScreen = terminal.GetScreenText();
            secondTerminalHasPrompt = secondTerminalScreen.Contains("$") || 
                                      secondTerminalScreen.Contains("#") ||
                                      secondTerminalScreen.Contains("bash");
            
            TestCaptureHelper.Capture(terminal.CreateSnapshot(), "diag3-second-terminal");
            TestContext.Current.SendDiagnosticMessage($"Second terminal has prompt: {secondTerminalHasPrompt}");
        }
        finally
        {
            await TestCaptureHelper.CaptureCastAsync(recorder, "diag3-recording", TestContext.Current.CancellationToken);
            
            foreach (var session in terminals)
            {
                await session.Terminal.DisposeAsync();
            }
            
            appRef?.RequestStop();
        }
        
        await runTask;
        
        // Assert - Document the bug
        TestContext.Current.AddAttachment("first-terminal-screen.txt", firstTerminalScreen);
        TestContext.Current.AddAttachment("second-terminal-screen.txt", secondTerminalScreen);
        
        // First terminal should work
        Assert.True(firstTerminalHasPrompt, 
            $"First terminal should show prompt.\nScreen:\n{firstTerminalScreen[..Math.Min(firstTerminalScreen.Length, 500)]}");
        
        // THE BUG: Second terminal does NOT show prompt (empty content)
        // This test documents the bug by checking that it DOES occur
        // When the bug is fixed, this assertion should be changed to:
        //   Assert.True(secondTerminalHasPrompt, ...)
        
        // For now, verify the bug exists:
        TestContext.Current.SendDiagnosticMessage("=== BUG DOCUMENTATION ===");
        TestContext.Current.SendDiagnosticMessage($"Second terminal has prompt: {secondTerminalHasPrompt}");
        TestContext.Current.SendDiagnosticMessage($"Expected: true");
        TestContext.Current.SendDiagnosticMessage($"Second terminal content sample:");
        TestContext.Current.SendDiagnosticMessage(secondTerminalScreen[..Math.Min(secondTerminalScreen.Length, 500)]);
        
        // This will FAIL when the bug is fixed (which is good - we'll know to update the test)
        Assert.True(secondTerminalHasPrompt,
            "BUG CONFIRMED: Second terminal shows empty content (no bash prompt). " +
            "The terminal border renders with 'Terminal 2' title but the PTY content is not displayed. " +
            $"First terminal worked: {firstTerminalHasPrompt}. " +
            "See attached screen captures for evidence.");
    }
}

/// <summary>
/// Simple record to track terminal sessions.
/// </summary>
internal record TerminalSession(int Id, Hex1bTerminal Terminal, TerminalWidgetHandle Handle);
