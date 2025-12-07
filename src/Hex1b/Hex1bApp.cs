using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public class Hex1bApp : IDisposable
{
    private readonly Func<CancellationToken, Task<Hex1bWidget>> _rootComponent;
    private readonly Func<Hex1bTheme>? _themeProvider;
    private readonly IHex1bTerminal _terminal;
    private readonly Hex1bRenderContext _context;
    private readonly bool _ownsTerminal;
    private Hex1bNode? _rootNode;

    /// <summary>
    /// Creates a Hex1bApp with a custom terminal implementation and optional theme.
    /// </summary>
    public Hex1bApp(Func<CancellationToken, Task<Hex1bWidget>> rootComponent, IHex1bTerminal terminal, Hex1bTheme? theme = null, bool ownsTerminal = false)
    {
        _rootComponent = rootComponent;
        _terminal = terminal;
        _context = new Hex1bRenderContext(terminal, theme);
        _ownsTerminal = ownsTerminal;
    }

    /// <summary>
    /// Creates a Hex1bApp with a custom terminal implementation and a dynamic theme provider.
    /// The theme provider is called on each render to get the current theme.
    /// </summary>
    public Hex1bApp(Func<CancellationToken, Task<Hex1bWidget>> rootComponent, IHex1bTerminal terminal, Func<Hex1bTheme> themeProvider, bool ownsTerminal = false)
    {
        _rootComponent = rootComponent;
        _themeProvider = themeProvider;
        _terminal = terminal;
        _context = new Hex1bRenderContext(terminal, themeProvider());
        _ownsTerminal = ownsTerminal;
    }

    /// <summary>
    /// Creates a Hex1bApp with the default console terminal and optional theme.
    /// </summary>
    public Hex1bApp(Func<CancellationToken, Task<Hex1bWidget>> rootComponent, Hex1bTheme? theme = null)
        : this(rootComponent, new ConsoleHex1bTerminal(), theme, ownsTerminal: true)
    {
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _context.EnterAlternateScreen();
        try
        {
            // Initial render
            await RenderFrameAsync(cancellationToken);

            // React to input events - only render when input is received
            await foreach (var inputEvent in _terminal.InputEvents.ReadAllAsync(cancellationToken))
            {
                // Dispatch input to the root node
                _rootNode?.HandleInput(inputEvent);

                // Re-render after handling input (state may have changed)
                await RenderFrameAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, exit gracefully
        }
        finally
        {
            // Always exit alternate buffer, even on error
            _context.ExitAlternateScreen();
        }
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        // Update theme if we have a dynamic theme provider
        if (_themeProvider != null)
        {
            _context.Theme = _themeProvider();
        }

        // Step 1: Call the root component to get the widget tree
        var widgetTree = await _rootComponent(cancellationToken);

        // Step 2: Reconcile - update the node tree to match the widget tree
        _rootNode = Reconcile(_rootNode, widgetTree);

        // Step 3: Layout - measure and arrange the node tree
        if (_rootNode != null)
        {
            var terminalSize = new Size(_context.Width, _context.Height);
            var constraints = Constraints.Tight(terminalSize);
            _rootNode.Measure(constraints);
            _rootNode.Arrange(Rect.FromSize(terminalSize));
        }

        // Step 4: Render the node tree to the terminal
        _context.Clear();
        _rootNode?.Render(_context);
    }

    /// <summary>
    /// Reconciles a node with a widget, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private static Hex1bNode? Reconcile(Hex1bNode? existingNode, Hex1bWidget? widget, Hex1bNode? parent = null)
    {
        if (widget is null)
        {
            return null;
        }

        // For now, simple reconciliation:
        // If the node type matches the widget type, update it.
        // Otherwise, create a new node.

#pragma warning disable HEX1B001 // Experimental Navigator API
        Hex1bNode node = widget switch
        {
            TextBlockWidget textWidget => ReconcileTextBlock(existingNode as TextBlockNode, textWidget),
            TextBoxWidget textBoxWidget => ReconcileTextBox(existingNode as TextBoxNode, textBoxWidget),
            ButtonWidget buttonWidget => ReconcileButton(existingNode as ButtonNode, buttonWidget),
            ListWidget listWidget => ReconcileList(existingNode as ListNode, listWidget),
            SplitterWidget splitterWidget => ReconcileSplitter(existingNode as SplitterNode, splitterWidget),
            VStackWidget vStackWidget => ReconcileVStack(existingNode as VStackNode, vStackWidget),
            HStackWidget hStackWidget => ReconcileHStack(existingNode as HStackNode, hStackWidget),
            NavigatorWidget navigatorWidget => ReconcileNavigator(existingNode as NavigatorNode, navigatorWidget),
            _ => throw new NotSupportedException($"Unknown widget type: {widget.GetType()}")
        };
#pragma warning restore HEX1B001

        // Set parent and shortcuts on the reconciled node
        node.Parent = parent;
        node.Shortcuts = widget.Shortcuts ?? [];

        return node;
    }

    private static TextBlockNode ReconcileTextBlock(TextBlockNode? existingNode, TextBlockWidget widget)
    {
        var node = existingNode ?? new TextBlockNode();
        node.Text = widget.Text;
        return node;
    }

    private static TextBoxNode ReconcileTextBox(TextBoxNode? existingNode, TextBoxWidget widget)
    {
        var node = existingNode ?? new TextBoxNode();
        node.State = widget.State;
        return node;
    }

    private static ButtonNode ReconcileButton(ButtonNode? existingNode, ButtonWidget widget)
    {
        var node = existingNode ?? new ButtonNode();
        node.Label = widget.Label;
        node.OnClick = widget.OnClick;
        return node;
    }

    private static VStackNode ReconcileVStack(VStackNode? existingNode, VStackWidget widget)
    {
        var node = existingNode ?? new VStackNode();

        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < widget.Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = Reconcile(existingChild, widget.Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;
        
        // Pass size hints from widget to node
        node.ChildHeightHints = widget.ChildHeightHints?.ToList() ?? [];

        // Invalidate focus cache since children changed
        node.InvalidateFocusCache();

        // Set initial focus on first focusable if this is a new node
        if (existingNode is null)
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    private static void SetNodeFocus(Hex1bNode node, bool focused)
    {
        switch (node)
        {
            case TextBoxNode textBox:
                textBox.IsFocused = focused;
                break;
            case ButtonNode button:
                button.IsFocused = focused;
                break;
            case ListNode list:
                list.IsFocused = focused;
                break;
        }
    }

    private static ListNode ReconcileList(ListNode? existingNode, ListWidget widget)
    {
        var node = existingNode ?? new ListNode();
        node.State = widget.State;
        return node;
    }

    private static SplitterNode ReconcileSplitter(SplitterNode? existingNode, SplitterWidget widget)
    {
        var node = existingNode ?? new SplitterNode();
        node.Left = Reconcile(node.Left, widget.Left, node);
        node.Right = Reconcile(node.Right, widget.Right, node);
        node.LeftWidth = widget.LeftWidth;
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus if this is a new node
        if (existingNode is null)
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    private static HStackNode ReconcileHStack(HStackNode? existingNode, HStackWidget widget)
    {
        var node = existingNode ?? new HStackNode();

        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < widget.Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = Reconcile(existingChild, widget.Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;
        
        // Pass size hints from widget to node
        node.ChildWidthHints = widget.ChildWidthHints?.ToList() ?? [];

        return node;
    }

#pragma warning disable HEX1B001 // Experimental API
    private static NavigatorNode ReconcileNavigator(NavigatorNode? existingNode, NavigatorWidget widget)
    {
        var node = existingNode ?? new NavigatorNode();
        node.State = widget.State;

        // Detect if the route has changed
        var newRouteId = widget.State.CurrentRoute.Id;
        var routeChanged = node.CurrentRouteId != newRouteId;
        
        // Check if we have a pending focus restore (from a pop)
        var pendingFocusRestore = widget.State.PendingFocusRestore;
        // Check if we need to save focus to a previous entry (from a push)
        var entryToSaveFocusTo = widget.State.EntryToSaveFocusTo;
        widget.State.ClearPendingFocusRestore();
        
        node.CurrentRouteId = newRouteId;

        // If route changed, save focus index to the previous entry and clear focus from old child
        if (routeChanged && node.CurrentChild != null)
        {
            // Save the current focus index to the entry we're navigating away from (only on push)
            if (entryToSaveFocusTo != null)
            {
                var oldFocusables = node.CurrentChild.GetFocusableNodes().ToList();
                Console.Error.WriteLine($"[Navigator] Route changed, saving focus. Old focusables count: {oldFocusables.Count}");
                bool foundFocused = false;
                for (int i = 0; i < oldFocusables.Count; i++)
                {
                    var isFocused = IsNodeFocused(oldFocusables[i]);
                    Console.Error.WriteLine($"[Navigator]   [{i}] {oldFocusables[i].GetType().Name}: IsFocused={isFocused}");
                    if (isFocused)
                    {
                        entryToSaveFocusTo.SavedFocusIndex = i;
                        foundFocused = true;
                        Console.Error.WriteLine($"[Navigator] Saved focus index: {i}");
                        break;
                    }
                }
                if (!foundFocused)
                {
                    Console.Error.WriteLine($"[Navigator] WARNING: No focused element found!");
                }
            }

            foreach (var focusable in node.CurrentChild.GetFocusableNodes())
            {
                SetNodeFocus(focusable, false);
            }
            // Force creation of new child by not passing existing
            node.CurrentChild = null;
        }

        // Build the current route's widget and reconcile it as the child
        var currentWidget = widget.State.BuildCurrentWidget();
        node.CurrentChild = Reconcile(node.CurrentChild, currentWidget, node);

        // Set focus based on whether we're returning from pop or navigating forward
        if (existingNode is null || routeChanged)
        {
            var focusables = node.GetFocusableNodes().ToList();
            Console.Error.WriteLine($"[Navigator] Setting focus. Focusables count: {focusables.Count}, pendingFocusRestore: {pendingFocusRestore}");
            if (focusables.Count > 0)
            {
                // Clear all existing focus first
                foreach (var focusable in focusables)
                {
                    SetNodeFocus(focusable, false);
                }
                
                int focusIndex = 0;
                
                // If returning from pop, restore saved focus index
                if (pendingFocusRestore.HasValue && pendingFocusRestore.Value < focusables.Count)
                {
                    focusIndex = pendingFocusRestore.Value;
                    Console.Error.WriteLine($"[Navigator] Restoring focus to index: {focusIndex}");
                }
                
                SetNodeFocus(focusables[focusIndex], true);
                Console.Error.WriteLine($"[Navigator] Set focus to: {focusables[focusIndex].GetType().Name}");
                
                // After setting focus, sync the internal focus index on container nodes
                if (node.CurrentChild != null)
                {
                    SyncContainerFocusIndices(node.CurrentChild);
                }
            }
        }

        return node;
    }

    private static void SyncContainerFocusIndices(Hex1bNode node)
    {
        // Recursively sync focus indices on all container nodes
        switch (node)
        {
            case VStackNode vstack:
                vstack.SyncFocusIndex();
                foreach (var child in vstack.Children)
                {
                    SyncContainerFocusIndices(child);
                }
                break;
            case HStackNode hstack:
                foreach (var child in hstack.Children)
                {
                    SyncContainerFocusIndices(child);
                }
                break;
            case SplitterNode splitter:
                splitter.SyncFocusIndex();
                if (splitter.Left != null) SyncContainerFocusIndices(splitter.Left);
                if (splitter.Right != null) SyncContainerFocusIndices(splitter.Right);
                break;
            case NavigatorNode navigator:
                if (navigator.CurrentChild != null)
                {
                    SyncContainerFocusIndices(navigator.CurrentChild);
                }
                break;
        }
    }

    private static bool IsNodeFocused(Hex1bNode node)
    {
        return node switch
        {
            TextBoxNode textBox => textBox.IsFocused,
            ButtonNode button => button.IsFocused,
            ListNode list => list.IsFocused,
            _ => false
        };
    }
#pragma warning restore HEX1B001

    public void Dispose()
    {
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
