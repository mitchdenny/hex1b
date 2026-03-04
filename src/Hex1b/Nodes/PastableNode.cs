using System.Diagnostics;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// A transparent container node that intercepts bracketed paste events.
/// Renders no chrome — layout and rendering pass through to the child.
/// </summary>
public sealed class PastableNode : Hex1bNode
{
    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; internal set; }

    /// <summary>
    /// The async action to execute when paste data arrives.
    /// </summary>
    public Func<PasteEventArgs, Task>? PasteAction { get; internal set; }

    /// <summary>
    /// Maximum paste size in characters. Null means unlimited.
    /// </summary>
    public int? MaxSize { get; internal set; }

    /// <summary>
    /// Timeout for the paste operation. Null means no timeout.
    /// </summary>
    public TimeSpan? PasteTimeout { get; internal set; }

    public override bool IsFocusable => false;

    public override IEnumerable<Hex1bNode> GetChildren()
        => Child != null ? [Child] : [];

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
            context.RenderChild(Child);
    }

    /// <summary>
    /// Handles paste by invoking the paste action with optional maxSize/timeout enforcement.
    /// </summary>
    public override async Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
    {
        if (PasteAction == null)
            return InputResult.NotHandled;

        var paste = pasteEvent.Paste;

        // Start enforcement tasks if limits are set
        var enforcementCts = new CancellationTokenSource();

        if (PasteTimeout.HasValue)
        {
            _ = EnforceTimeoutAsync(paste, PasteTimeout.Value, enforcementCts.Token);
        }

        if (MaxSize.HasValue)
        {
            _ = EnforceMaxSizeAsync(paste, MaxSize.Value, enforcementCts.Token);
        }

        try
        {
            await PasteAction(new PasteEventArgs(paste));
        }
        finally
        {
            enforcementCts.Cancel();
            enforcementCts.Dispose();
        }

        return InputResult.Handled;
    }

    private static async Task EnforceTimeoutAsync(PasteContext paste, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            await Task.Delay(timeout, ct);
            if (!paste.IsCompleted && !paste.IsCancelled)
                paste.Cancel();
        }
        catch (OperationCanceledException)
        {
            // Enforcement was cancelled (paste completed before timeout)
        }
    }

    private static async Task EnforceMaxSizeAsync(PasteContext paste, int maxSize, CancellationToken ct)
    {
        try
        {
            // Poll TotalCharactersWritten instead of consuming from the channel
            while (!ct.IsCancellationRequested && !paste.IsCompleted && !paste.IsCancelled)
            {
                if (paste.TotalCharactersWritten > maxSize)
                {
                    paste.Cancel();
                    return;
                }
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Enforcement was cancelled
        }
    }
}
