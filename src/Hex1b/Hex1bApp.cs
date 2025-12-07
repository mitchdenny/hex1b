using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public class Hex1bApp : IDisposable
{
    private readonly Func<CancellationToken, Task<Hex1bWidget>> _rootComponent;
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

        Hex1bNode node = widget switch
        {
            TextBlockWidget textWidget => ReconcileTextBlock(existingNode as TextBlockNode, textWidget),
            TextBoxWidget textBoxWidget => ReconcileTextBox(existingNode as TextBoxNode, textBoxWidget),
            ButtonWidget buttonWidget => ReconcileButton(existingNode as ButtonNode, buttonWidget),
            ListWidget listWidget => ReconcileList(existingNode as ListNode, listWidget),
            SplitterWidget splitterWidget => ReconcileSplitter(existingNode as SplitterNode, splitterWidget),
            VStackWidget vStackWidget => ReconcileVStack(existingNode as VStackNode, vStackWidget),
            HStackWidget hStackWidget => ReconcileHStack(existingNode as HStackNode, hStackWidget),
            _ => throw new NotSupportedException($"Unknown widget type: {widget.GetType()}")
        };

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

    public void Dispose()
    {
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
