using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the MenuBar widget and menu navigation.
/// These tests verify the full keyboard and mouse navigation patterns
/// for menus, submenus, and menu item activation.
/// </summary>
public class MenuBarIntegrationTests
{
    #region Helper Methods
    
    /// <summary>
    /// Creates a standard menu bar for testing with callbacks to track actions.
    /// Uses the fluent API.
    /// </summary>
    private static Hex1bWidget CreateTestMenuBar(
        WidgetContext<RootWidget> ctx,
        Action<string> onAction,
        bool includeSubmenus = false,
        bool includeDisabledItems = false)
    {
        return ctx.VStack(main => [
            ctx.MenuBar(m => BuildMenus(m, onAction, includeSubmenus, includeDisabledItems)),
            ctx.Text("Content area - click here to dismiss menus")
        ]);
    }
    
    private static IEnumerable<MenuWidget> BuildMenus(
        MenuContext m,
        Action<string> onAction,
        bool includeSubmenus,
        bool includeDisabledItems)
    {
        yield return m.Menu("&File", m => BuildFileMenu(m, onAction, includeSubmenus, includeDisabledItems));
        yield return m.Menu("&Edit", m => [
            m.MenuItem("&Undo").OnActivated(e => onAction("Edit > Undo")),
            m.MenuItem("&Redo").OnActivated(e => onAction("Edit > Redo")),
            m.Separator(),
            m.MenuItem("Cu&t").OnActivated(e => onAction("Edit > Cut")),
            m.MenuItem("&Copy").OnActivated(e => onAction("Edit > Copy")),
            m.MenuItem("&Paste").OnActivated(e => onAction("Edit > Paste"))
        ]);
        yield return m.Menu("&Help", m => [
            m.MenuItem("&About").OnActivated(e => onAction("Help > About"))
        ]);
    }
    
    private static IEnumerable<IMenuChild> BuildFileMenu(
        MenuContext m,
        Action<string> onAction,
        bool includeSubmenus,
        bool includeDisabledItems)
    {
        yield return m.MenuItem("&New").OnActivated(e => onAction("File > New"));
        yield return m.MenuItem("&Open").OnActivated(e => onAction("File > Open"));
        yield return m.Separator();
        
        if (includeSubmenus)
        {
            yield return m.Menu("&Recent", m => [
                m.MenuItem("Doc1.txt").OnActivated(e => onAction("File > Recent > Doc1.txt")),
                m.MenuItem("Doc2.txt").OnActivated(e => onAction("File > Recent > Doc2.txt"))
            ]);
        }
        else
        {
            yield return m.MenuItem("&Recent").Disabled();
        }
        
        yield return m.Separator();
        yield return m.MenuItem("&Save").OnActivated(e => onAction("File > Save"));
        
        if (includeDisabledItems)
        {
            yield return m.MenuItem("Save &As").Disabled();
        }
        else
        {
            yield return m.MenuItem("Save &As").OnActivated(e => onAction("File > Save As"));
        }
        
        yield return m.Separator();
        yield return m.MenuItem("&Quit").OnActivated(e =>
        {
            onAction("File > Quit");
            e.Context.RequestStop();
        });
    }
    
    #endregion
    
    #region Menu Bar Focus and Opening
    
    [Fact]
    public async Task MenuBar_EnterKey_OpensFirstMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File") && s.ContainsText("Edit"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open the File menu (first focused menu)
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - File menu items should be visible
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("New"));
        Assert.True(snapshot.ContainsText("Open"));
        Assert.True(snapshot.ContainsText("Save"));
    }
    
    [Fact]
    public async Task MenuBar_DownArrow_OpensMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Down()  // Down arrow should also open the menu
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("New"));
    }
    
    [Fact]
    public async Task MenuBar_AltF_OpensFileMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Alt().Key(Hex1bKey.F)  // Alt+F accelerator
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open via Alt+F")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("New"));
    }
    
    [Fact]
    public async Task MenuBar_AltE_OpensEditMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Edit"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Alt().Key(Hex1bKey.E)  // Alt+E accelerator
            .WaitUntil(s => s.ContainsText("Undo") && s.ContainsText("Redo"), TimeSpan.FromSeconds(2), "Edit menu to open via Alt+E")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("Cut"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Copy"));
    }
    
    [Fact]
    public async Task MenuBar_RightArrow_NavigatesToNextMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Start at File, arrow right to Edit, then open it
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Right()  // Move from File to Edit
            .Enter()  // Open Edit menu
            .WaitUntil(s => s.ContainsText("Undo") && s.ContainsText("Copy"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Edit menu should be open, not File menu
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Undo"));
        Assert.True(snapshot.ContainsText("Cut"));
    }
    
    [Fact]
    public async Task MenuBar_LeftArrow_NavigatesToPreviousMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Navigate right twice to Help, then left back to Edit, then open it
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Help"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Right()  // File -> Edit
            .Right()  // Edit -> Help
            .Left()   // Help -> Edit
            .Enter()  // Open Edit menu
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("Undo"));
    }
    
    #endregion
    
    #region Menu Item Navigation and Activation
    
    [Fact]
    public async Task MenuItem_DownArrow_NavigatesToNextItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open menu, navigate down, verify selection, then activate
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // Navigate from New to Open
            .WaitUntil(s => IsMenuItemSelected(s, "Open"), TimeSpan.FromSeconds(2), "Open to be selected after Down arrow")
            .Enter()  // Activate Open
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - "Open" action should have been triggered
        Assert.Equal("File > Open", lastAction);
    }
    
    /// <summary>
    /// Checks if a menu item is selected by verifying it has the focused background color (white).
    /// </summary>
    private static bool IsMenuItemSelected(IHex1bTerminalRegion snapshot, string itemText)
    {
        // Find the text position
        var positions = snapshot.FindText(itemText);
        if (positions.Count == 0)
            return false;
        
        // Check the first occurrence - the first character should have white background
        var (line, column) = positions[0];
        var cell = snapshot.GetCell(column, line);
        
        // The focused menu item should have white background (Hex1bColor.White = RGB 255,255,255)
        if (cell.Background is not null)
        {
            return cell.Background.Value.R == 255 && 
                   cell.Background.Value.G == 255 && 
                   cell.Background.Value.B == 255;
        }
        
        return false;
    }
    
    [Fact]
    public async Task MenuItem_Tab_NavigatesToNextItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open menu, navigate with Tab, verify selection, then activate
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Tab()    // Navigate from New to Open using Tab
            .WaitUntil(s => IsMenuItemSelected(s, "Open"), TimeSpan.FromSeconds(2), "Open to be selected after Tab")
            .Enter()  // Activate Open
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - "Open" action should have been triggered
        Assert.Equal("File > Open", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_UpArrow_NavigatesToPreviousItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open menu, navigate down twice then up once, activate
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // New -> Open
            .Down()   // Open -> (skip separator) -> Recent or Save
            .Up()     // Back to Open
            .Enter()  // Activate Open
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("File > Open", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_Enter_ActivatesItemAndClosesMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Enter()  // Activate "New" (first item)
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Action was triggered and menu closed
        Assert.Equal("File > New", lastAction);
        Assert.False(terminal.CreateSnapshot().ContainsText("Open")); // Menu closed
    }
    
    [Fact]
    public async Task MenuItem_Spacebar_ActivatesItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Key(Hex1bKey.Spacebar)  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Key(Hex1bKey.Spacebar)  // Activate "New" with spacebar
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("File > New", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_Escape_ClosesMenuWithoutActivating()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // Navigate to Open
            .Escape() // Dismiss without activating
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - No action should have been triggered
        Assert.Equal("", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_LeftArrow_ClosesMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Left()   // Close menu (back navigation)
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Menu should be closed, no action triggered
        Assert.Equal("", lastAction);
        Assert.False(terminal.CreateSnapshot().ContainsText("Open"));
    }
    
    [Fact]
    public async Task MenuItem_UpArrowOnFirstItem_ClosesMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu (starts on New), press Up which should close menu
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu, focus is on "New" (first item)
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Up()     // On first item, Up should close the menu
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Menu should be closed, no action triggered
        Assert.Equal("", lastAction);
        Assert.False(terminal.CreateSnapshot().ContainsText("Open"));
    }
    
    [Fact]
    public async Task MenuItem_UpArrowOnNonFirstItem_NavigatesPrevious()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, go down to Open, then Up to New, press Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // Move to Open
            .Up()     // Move back to New - should not close
            .Enter()  // Activate New
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - "New" should have been activated
        Assert.Equal("File > New", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_UpArrowOnFirstItem_FocusStaysOnMenuTrigger()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Navigate to Edit menu, open it, press Up to close, verify focus stays on Edit
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Right()  // Navigate to Edit menu (without opening)
            .Enter()  // Open Edit menu, focus is on "Undo" (first item)
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Up()     // On first item, Up should close the menu
            .WaitUntil(s => !s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "menu to close")
            .Down()   // If focus is on Edit, this should reopen Edit menu
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to reopen")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Edit menu should be open again (proving focus stayed on Edit)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Undo"));
    }
    
    [Fact]
    public async Task MenuItem_AcceleratorKey_ActivatesItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, press 'O' to activate Open
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Key(Hex1bKey.O)  // Accelerator for "Open"
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("File > Open", lastAction);
    }
    
    #endregion
    
    #region Submenu Navigation
    
    [Fact]
    public async Task Submenu_RightArrow_OpensSubmenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, navigate to Recent submenu, open it with right arrow
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // New -> Open
            .Down()   // Open -> Recent (skip separator)
            .Right()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Submenu items should be visible
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Doc1.txt"));
        Assert.True(snapshot.ContainsText("Doc2.txt"));
    }
    
    [Fact]
    public async Task Submenu_Enter_OpensSubmenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, navigate to Recent, open with Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent (New -> Open -> Recent)
            .Enter()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("Doc1.txt"));
    }
    
    [Fact]
    public async Task Submenu_LeftArrow_ClosesSubmenuReturnsToParent()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open submenu, then close it with left arrow
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent (New -> Open -> Recent)
            .Right()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Left()   // Close submenu, return to parent
            .WaitUntil(s => !s.ContainsText("Doc1.txt") && s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "submenu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Submenu closed but parent menu still open
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsText("Doc1.txt")); // Submenu closed
        Assert.True(snapshot.ContainsText("Save"));       // Parent menu still visible
    }
    
    [Fact]
    public async Task Submenu_LeftArrow_FocusReturnsToSubmenuTrigger()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open submenu, close it with left arrow, verify focus by pressing right to reopen
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent (New -> Open -> Recent)
            .Right()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Left()   // Close submenu, return to parent - focus should be on "Recent"
            .WaitUntil(s => !s.ContainsText("Doc1.txt") && s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "submenu to close")
            .Right()  // If focus is on Recent, this should reopen the submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to reopen")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Submenu should be open again, proving focus was on "Recent"
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Doc1.txt")); // Submenu is open
    }
    
    [Fact]
    public async Task Submenu_ActivateItem_ClosesAllMenus()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Navigate into submenu and activate an item
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent (New -> Open -> Recent)
            .Right()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Enter()  // Activate Doc1.txt
            .WaitUntil(s => !s.ContainsText("Doc1.txt") && !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "all menus to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("File > Recent > Doc1.txt", lastAction);
        // All menus should be closed
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsText("Doc1.txt"));
        Assert.False(snapshot.ContainsText("Save"));
    }
    
    #endregion
    
    #region Mouse Navigation
    
    [Fact]
    public async Task Menu_ClickOnTrigger_OpensMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act - Click on "File" menu trigger
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .ClickAt(2, 0, MouseButton.Left)  // Click on "File" (assuming it starts near x=1)
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(terminal.CreateSnapshot().ContainsText("New"));
    }
    
    [Fact]
    public async Task Menu_ClickOnItem_ActivatesItem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act - Open menu with keyboard, then click on an item
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu with keyboard
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            // Click on "Open" item (should be on line 2 or 3 depending on border)
            .ClickAt(3, 2, MouseButton.Left)  // Approximate position of Open
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Should have activated some item (position may vary)
        Assert.NotEqual("", lastAction);
    }
    
    [Fact]
    public async Task Menu_ClickAwayOnBackdrop_ClosesMenu()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act - Open menu, then click away on backdrop
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            // Click on backdrop (bottom right corner, far from menu)
            .ClickAt(70, 20, MouseButton.Left)
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close after click-away")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - No action should be triggered, menu should be closed
        Assert.Equal("", lastAction);
        Assert.False(terminal.CreateSnapshot().ContainsText("Save"));
    }
    
    [Fact]
    public async Task Menu_ClickAwayThenClickAnotherMenu_Works()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act - Open Edit menu, click away, then click on Help menu
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File") && s.ContainsText("Edit") && s.ContainsText("Help"), TimeSpan.FromSeconds(2), "menu bar to render")
            // Click on Edit menu to open it (Edit is around position 6-9)
            .ClickAt(7, 0, MouseButton.Left)
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            // Click away on backdrop to close
            .ClickAt(70, 20, MouseButton.Left)
            .WaitUntil(s => !s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "menu to close after click-away")
            // Now click on Help menu (Help is around position 11-14)
            .ClickAt(13, 0, MouseButton.Left)
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Help menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("About"));
    }
    
    #endregion
    
    #region Disabled Items
    
    [Fact]
    public async Task DisabledItem_Enter_DoesNotActivate()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeDisabledItems: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, navigate to disabled "Save As", try to activate
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Save As"), TimeSpan.FromSeconds(2), "File menu to open")
            // Navigate to Save As (disabled)
            .Down().Down().Down().Down().Down().Down()  // Skip to Save As
            .Enter()  // Try to activate disabled item
            .Wait(100)  // Wait to see if anything happens
            .Escape()  // Close menu
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Action should NOT have been triggered for disabled item
        Assert.NotEqual("File > Save As", lastAction);
    }
    
    #endregion
    
    #region Focus Restoration
    
    [Fact]
    public async Task Menu_AfterClose_FocusRestoresToMenuBar()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open menu, close it, then open again to verify focus returned
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Escape() // Close menu
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            .Enter()  // Open again - this proves focus returned to menu bar
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to reopen")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - If we got here, focus was restored correctly
        Assert.True(terminal.CreateSnapshot().ContainsText("New"));
    }
    
    #endregion
    
    #region Complex Workflows
    
    [Fact]
    public async Task CompleteWorkflow_NavigateMultipleMenusAndActivateItems()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var actions = new List<string>();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => actions.Add(a))),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Perform multiple menu operations
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            // First: File > New
            .Enter()
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Enter()  // Activate New
            .WaitUntil(s => !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "menu to close")
            // Second: Navigate to Edit, activate Undo
            .Right()  // Move to Edit
            .Enter()
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Enter()  // Activate Undo
            .WaitUntil(s => !s.ContainsText("Paste"), TimeSpan.FromSeconds(2), "menu to close")
            // Third: Use Alt+H for Help
            .Alt().Key(Hex1bKey.H)
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open")
            .Enter()  // Activate About
            .WaitUntil(s => !s.ContainsText("About"), TimeSpan.FromSeconds(2), "menu to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - All three actions should have been recorded
        Assert.Contains("File > New", actions);
        Assert.Contains("Edit > Undo", actions);
        Assert.Contains("Help > About", actions);
    }
    
    #endregion
    
    #region Focus Debugging Tests
    
    [Fact]
    public void Debug_BackdropFocusDelegation_ToMenuItems()
    {
        // Arrange - Create the node hierarchy manually
        var menuPopupNode = new MenuPopupNode();
        var anchoredNode = new AnchoredNode { Child = menuPopupNode };
        var backdropNode = new BackdropNode { Child = anchoredNode };
        
        // Create menu items as if reconciled
        var menuItemNode1 = new MenuItemNode { Label = "New", Parent = menuPopupNode };
        var menuItemNode2 = new MenuItemNode { Label = "Open", Parent = menuPopupNode };
        menuPopupNode.ChildNodes.Add(menuItemNode1);
        menuPopupNode.ChildNodes.Add(menuItemNode2);
        
        // Act - Get focusable nodes from backdrop
        var focusables = backdropNode.GetFocusableNodes().ToList();
        
        // Assert - Should include backdrop and menu items
        Assert.True(focusables.Count >= 3, $"Expected at least 3 focusables (backdrop + 2 items), got {focusables.Count}");
        Assert.Contains(menuItemNode1, focusables);
        Assert.Contains(menuItemNode2, focusables);
        
        // Act - Set focus on backdrop (simulating what ZStack does)
        backdropNode.IsFocused = true;
        
        // Assert - Focus should have been delegated to first menu item
        Assert.True(menuItemNode1.IsFocused, "First menu item should have focus after setting backdrop focus");
    }
    
    [Fact]
    public async Task Debug_FirstMenuItem_HasFocus_WhenMenuOpens()
    {
        // This test verifies that when a menu opens, the first menu item automatically gets focus
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, _ => { })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        Hex1bNode? focusedAfterMenuOpen = null;
        int focusableCount = 0;
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => {
                // Capture focus state when menu is open
                if (s.ContainsText("New"))
                {
                    focusedAfterMenuOpen = app.FocusedNode;
                    focusableCount = app.Focusables.Count;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(2), "File menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Assert - Focus should be on the first MenuItemNode
        Assert.True(focusableCount > 0, $"Expected focusables in the ring, got {focusableCount}");
        Assert.NotNull(focusedAfterMenuOpen);
        Assert.IsType<MenuItemNode>(focusedAfterMenuOpen);
        Assert.Equal("New", ((MenuItemNode)focusedAfterMenuOpen).Label);
    }
    
    [Fact]
    public async Task Debug_DownArrow_MovesFocus_WhenMenuOpen()
    {
        // This test checks if Down arrow moves focus from "New" to "Open"
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, _ => { })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        Hex1bNode? focusedAfterMenuOpen = null;
        Hex1bNode? focusedAfterDownArrow = null;
        List<(string type, string? label)> focusablesBefore = new();
        List<(string type, string? label, bool focused)> focusablesAfter = new();
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => {
                if (s.ContainsText("New"))
                {
                    focusedAfterMenuOpen = app.FocusedNode;
                    focusablesBefore = app.Focusables
                        .Select(n => (n.GetType().Name, (n as MenuItemNode)?.Label))
                        .ToList();
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // Navigate from New to Open
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(_ => {
                focusedAfterDownArrow = app.FocusedNode;
                focusablesAfter = app.Focusables
                    .Select(n => (n.GetType().Name, (n as MenuItemNode)?.Label, n.IsFocused))
                    .ToList();
                return true;
            }, TimeSpan.FromSeconds(2), "capture state after Down arrow")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Debug output
        var focusedAfterMenuOpenLabel = (focusedAfterMenuOpen as MenuItemNode)?.Label ?? focusedAfterMenuOpen?.GetType().Name ?? "null";
        var focusedAfterDownArrowLabel = (focusedAfterDownArrow as MenuItemNode)?.Label ?? focusedAfterDownArrow?.GetType().Name ?? "null";
        
        // Check which nodes have IsFocused = true
        var focusedNodes = focusablesAfter.Where(f => f.focused).ToList();
        
        Assert.NotNull(focusedAfterDownArrow);
        Assert.True(focusedAfterMenuOpen != focusedAfterDownArrow, 
            $"Focus didn't move. Before Down: {focusedAfterMenuOpenLabel}, After Down: {focusedAfterDownArrowLabel}. " +
            $"LastFocusChange: {app.LastFocusChange ?? "null"}. " +
            $"LastPathDebug: {app.LastPathDebug ?? "null"}. " +
            $"ZStackFocusDebug: {Hex1b.Widgets.ZStackWidget.LastFocusDebug ?? "null"}. " +
            $"Focusables BEFORE: [{string.Join(", ", focusablesBefore.Select(f => f.type + (f.label != null ? ":" + f.label : "")))}]. " +
            $"Focusables AFTER with IsFocused: [{string.Join(", ", focusablesAfter.Select(f => f.type + (f.label != null ? ":" + f.label : "") + (f.focused ? "*" : "")))}].");
    }
    
    [Fact]
    public async Task Debug_Submenu_RightArrow_OpensSubmenu()
    {
        // Debug test for submenu opening
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, _ => { }, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        Hex1bNode? focusedBeforeRight = null;
        string? focusedParentType = null;
        string? lastPath = null;
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down()   // New -> Open
            .Down()   // Open -> Recent (skip separator)
            .Wait(TimeSpan.FromMilliseconds(100))
            .WaitUntil(_ => {
                focusedBeforeRight = app.FocusedNode;
                focusedParentType = focusedBeforeRight?.Parent?.GetType().Name;
                return true;
            }, TimeSpan.FromSeconds(1), "capture focus before Right")
            .Right()  // Should open Recent submenu
            .Wait(TimeSpan.FromMilliseconds(100))
            .WaitUntil(_ => {
                lastPath = app.LastPathDebug;
                return true;
            }, TimeSpan.FromSeconds(1), "capture path after Right")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        var focusedLabel = (focusedBeforeRight as MenuNode)?.Label ?? 
                           (focusedBeforeRight as MenuItemNode)?.Label ?? 
                           focusedBeforeRight?.GetType().Name ?? "null";
        var snapshot = terminal.CreateSnapshot();
        var hasDoc1 = snapshot.ContainsText("Doc1.txt");
        
        Assert.True(hasDoc1, 
            $"Submenu didn't open. Focus before Right: {focusedLabel}, Parent: {focusedParentType}. " +
            $"LastPath: {lastPath ?? "null"}. " +
            $"Screen has Doc1.txt: {hasDoc1}");
    }
    
    #endregion
    
    #region Cross-Menu Navigation (Arrow Keys Navigate Between Menus While Open)
    
    [Fact]
    public async Task MenuItem_RightArrow_OpensNextMenuWhenNoSubmenu()
    {
        // Arrange - When on a leaf menu item (no children), Right arrow should
        // close the current menu and open the next menu in the menu bar
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, then press Right to navigate to Edit menu
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Right()  // Should close File and open Edit
            .WaitUntil(s => s.ContainsText("Undo") && !s.ContainsText("Save"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Edit menu should be open (not File menu)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Undo"), "Edit menu should be visible");
        Assert.False(snapshot.ContainsText("Save"), "File menu should be closed");
    }
    
    [Fact]
    public async Task MenuItem_LeftArrow_OpensPreviousMenuWhenNoSubmenu()
    {
        // Arrange - When on a leaf menu item (no children), Left arrow should
        // close the current menu and open the previous menu in the menu bar
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Navigate to Edit menu, open it, then press Left to go back to File
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Edit"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Right()  // File -> Edit
            .Enter()  // Open Edit menu
            .WaitUntil(s => s.ContainsText("Undo") && s.ContainsText("Copy"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Left()   // Should close Edit and open File
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open after Left arrow")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - File menu should be open (not Edit menu)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("New"), "File menu should be visible");
        Assert.True(snapshot.ContainsText("Save"), "File menu should be visible");
        Assert.False(snapshot.ContainsText("Undo"), "Edit menu should be closed");
    }
    
    [Fact]
    public async Task MenuItem_RightArrow_WrapsAroundToFirstMenu()
    {
        // Arrange - When on the last menu (Help), Right arrow should wrap to File
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Navigate to Help menu, open it, then press Right to wrap to File
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Help"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Right().Right()  // File -> Edit -> Help
            .Enter()  // Open Help menu
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open")
            .Right()  // Should close Help and wrap to File
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open after wrapping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - File menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("New"), "File menu should be visible after wrap");
        Assert.False(snapshot.ContainsText("About"), "Help menu should be closed");
    }
    
    [Fact]
    public async Task MenuItem_LeftArrow_WrapsAroundToLastMenu()
    {
        // Arrange - When on the first menu (File), Left arrow should wrap to Help
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, then press Left to wrap to Help
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Left()   // Should close File and wrap to Help
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open after wrapping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Help menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("About"), "Help menu should be visible after wrap");
        Assert.False(snapshot.ContainsText("Save"), "File menu should be closed");
    }
    
    [Fact]
    public async Task MenuItem_RightArrow_ThenActivateItem_Works()
    {
        // Arrange - Navigate across menus with arrow keys, then activate an item
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File, Right to Edit, Down to Redo, Enter to activate
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Right()  // Navigate to Edit menu
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Down()   // Undo -> Redo
            .Enter()  // Activate Redo
            .WaitUntil(s => !s.ContainsText("Paste"), TimeSpan.FromSeconds(2), "menu to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Redo action should have been triggered
        Assert.Equal("Edit > Redo", lastAction);
    }
    
    [Fact]
    public async Task MenuItem_MultipleRightArrows_NavigatesAcrossMenus()
    {
        // Arrange - Press Right multiple times to navigate File -> Edit -> Help
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File, Right to Edit, Right to Help
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Right()  // Navigate to Edit menu
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Right()  // Navigate to Help menu
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Help menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("About"), "Help menu should be visible");
        Assert.False(snapshot.ContainsText("Save"), "File menu should be closed");
        Assert.False(snapshot.ContainsText("Paste"), "Edit menu should be closed");
    }
    
    [Fact]
    public async Task MenuItem_RightArrow_FromMiddleOfMenu_OpensNextMenu()
    {
        // Arrange - Navigate down within a menu first, then Right should still work
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File, Down twice (to get past first item), then Right to Edit
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate down a couple items
            .Wait(TimeSpan.FromMilliseconds(50))  // Allow focus to settle
            .Right()  // Navigate to Edit menu
            .WaitUntil(s => s.ContainsText("Undo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Edit menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Undo"), "Edit menu should be visible");
        Assert.False(snapshot.ContainsText("Save"), "File menu should be closed");
    }
    
    [Fact]
    public async Task SubmenuTrigger_LeftArrow_OpensPreviousMenu()
    {
        // Arrange - When focused on a submenu trigger (like "Recent" in File menu),
        // Left arrow should navigate to the previous menu in the menu bar (Help with wrap)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, navigate to "Recent" (submenu trigger), then Left
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent (New -> Open -> Recent)
            .Wait(TimeSpan.FromMilliseconds(50))
            .Left()   // Should navigate to Help menu (previous with wrap)
            .WaitUntil(s => s.ContainsText("About"), TimeSpan.FromSeconds(2), "Help menu to open after Left from submenu trigger")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Help menu should be open (not File menu)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("About"), "Help menu should be visible");
        Assert.False(snapshot.ContainsText("Save"), "File menu should be closed");
    }
    
    [Fact]
    public async Task SubmenuTrigger_RightArrow_OpensSubmenu()
    {
        // Arrange - When focused on a submenu trigger, Right arrow should open the submenu
        // (This tests that the new Left behavior doesn't break Right)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(CreateTestMenuBar(ctx, a => lastAction = a, includeSubmenus: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open File menu, navigate to "Recent", then Right to open submenu
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2), "menu bar to render")
            .Enter()  // Open File menu
            .WaitUntil(s => s.ContainsText("Recent"), TimeSpan.FromSeconds(2), "File menu to open")
            .Down().Down()  // Navigate to Recent
            .Right()  // Open Recent submenu
            .WaitUntil(s => s.ContainsText("Doc1.txt"), TimeSpan.FromSeconds(2), "Recent submenu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Recent submenu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Doc1.txt"), "Recent submenu should be visible");
        Assert.True(snapshot.ContainsText("Doc2.txt"), "Recent submenu should be visible");
    }
    
    #endregion
    
    #region Global Bindings
    
    [Fact]
    public async Task MenuBar_AltF_WorksWhenTextBoxFocused()
    {
        // Arrange - This tests global bindings: menu accelerators should work
        // even when focus is on a TextBox (not on the menu bar)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(main => [
                ctx.MenuBar(m => [
                    m.Menu("&File", m => [
                        m.MenuItem("&New").OnActivated(e => lastAction = "File > New"),
                        m.MenuItem("&Open").OnActivated(e => lastAction = "File > Open")
                    ]),
                    m.Menu("&Edit", m => [
                        m.MenuItem("&Undo").OnActivated(e => lastAction = "Edit > Undo")
                    ])
                ]),
                // A TextBox that can receive focus
                ctx.TextBox("Type here...")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Tab to the TextBox, then press Alt+F
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File") && s.ContainsText("Type here"), TimeSpan.FromSeconds(2), "menu bar and textbox to render")
            .Tab()  // Move focus to the TextBox
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(100), "focus to move")
            .Alt().Key(Hex1bKey.F)  // Global accelerator should still open File menu
            .WaitUntil(s => s.ContainsText("New") && s.ContainsText("Open"), TimeSpan.FromSeconds(2), "File menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - File menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("New"), "File menu item 'New' should be visible");
        Assert.True(snapshot.ContainsText("Open"), "File menu item 'Open' should be visible");
    }
    
    [Fact]
    public async Task MenuBar_AltE_WorksWhenButtonFocused()
    {
        // Arrange - Alt+E should open Edit menu even when a Button is focused
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastAction = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(main => [
                ctx.MenuBar(m => [
                    m.Menu("&File", m => [
                        m.MenuItem("&New").OnActivated(e => lastAction = "File > New")
                    ]),
                    m.Menu("&Edit", m => [
                        m.MenuItem("&Undo").OnActivated(e => lastAction = "Edit > Undo"),
                        m.MenuItem("&Redo").OnActivated(e => lastAction = "Edit > Redo")
                    ])
                ]),
                // A Button that can receive focus
                ctx.Button("Click Me").OnClick(e => lastAction = "Button clicked")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Tab to the Button, then press Alt+E
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File") && s.ContainsText("Click Me"), TimeSpan.FromSeconds(2), "menu bar and button to render")
            .Tab()  // Move focus to the Button
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(100), "focus to move")
            .Alt().Key(Hex1bKey.E)  // Global accelerator should open Edit menu
            .WaitUntil(s => s.ContainsText("Undo") && s.ContainsText("Redo"), TimeSpan.FromSeconds(2), "Edit menu to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Edit menu should be open
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Undo"), "Edit menu item 'Undo' should be visible");
        Assert.True(snapshot.ContainsText("Redo"), "Edit menu item 'Redo' should be visible");
    }
    
    #endregion
}
