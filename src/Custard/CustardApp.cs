using Custard.Widgets;

namespace Custard;

public class CustardApp : IDisposable
{
    private readonly Func<CancellationToken, Task<CustardWidget>> _rootComponent;
    private readonly ICustardTerminal _terminal;
    private readonly CustardRenderContext _context;
    private readonly bool _ownsTerminal;
    private CustardNode? _rootNode;

    /// <summary>
    /// Creates a CustardApp with a custom terminal implementation.
    /// </summary>
    public CustardApp(Func<CancellationToken, Task<CustardWidget>> rootComponent, ICustardTerminal terminal, bool ownsTerminal = false)
    {
        _rootComponent = rootComponent;
        _terminal = terminal;
        _context = new CustardRenderContext(terminal);
        _ownsTerminal = ownsTerminal;
    }

    /// <summary>
    /// Creates a CustardApp with the default console terminal.
    /// </summary>
    public CustardApp(Func<CancellationToken, Task<CustardWidget>> rootComponent)
        : this(rootComponent, new ConsoleCustardTerminal(), ownsTerminal: true)
    {
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _context.EnterAlternateScreen();
        try
        {
            // Main render loop
            while (!cancellationToken.IsCancellationRequested)
            {
                // Process any pending input events
                while (_terminal.InputEvents.TryRead(out var inputEvent))
                {
                    // Dispatch input to the root node (for now, no focus system)
                    _rootNode?.HandleInput(inputEvent);
                }

                // Render the current frame
                await RenderFrameAsync(cancellationToken);

                // Delay to control frame rate (~60 FPS)
                await Task.Delay(16, cancellationToken);
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

        // Step 3: Render the node tree to the terminal
        _context.Clear();
        _rootNode?.Render(_context);
    }

    /// <summary>
    /// Reconciles a node with a widget, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private static CustardNode? Reconcile(CustardNode? existingNode, CustardWidget? widget)
    {
        if (widget is null)
        {
            return null;
        }

        // For now, simple reconciliation:
        // If the node type matches the widget type, update it.
        // Otherwise, create a new node.

        return widget switch
        {
            TextBlockWidget textWidget => ReconcileTextBlock(existingNode as TextBlockNode, textWidget),
            TextBoxWidget textBoxWidget => ReconcileTextBox(existingNode as TextBoxNode, textBoxWidget),
            VStackWidget vStackWidget => ReconcileVStack(existingNode as VStackNode, vStackWidget),
            HStackWidget hStackWidget => ReconcileHStack(existingNode as HStackNode, hStackWidget),
            _ => throw new NotSupportedException($"Unknown widget type: {widget.GetType()}")
        };
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

    private static VStackNode ReconcileVStack(VStackNode? existingNode, VStackWidget widget)
    {
        var node = existingNode ?? new VStackNode();

        // Reconcile children
        var newChildren = new List<CustardNode>();
        for (int i = 0; i < widget.Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = Reconcile(existingChild, widget.Children[i]);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

        // Invalidate focus cache since children changed
        node.InvalidateFocusCache();

        // Set initial focus on first focusable if this is a new node
        if (existingNode is null)
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0 && focusables[0] is TextBoxNode firstTextBox)
            {
                firstTextBox.IsFocused = true;
            }
        }

        return node;
    }

    private static HStackNode ReconcileHStack(HStackNode? existingNode, HStackWidget widget)
    {
        var node = existingNode ?? new HStackNode();

        // Reconcile children
        var newChildren = new List<CustardNode>();
        for (int i = 0; i < widget.Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = Reconcile(existingChild, widget.Children[i]);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

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
