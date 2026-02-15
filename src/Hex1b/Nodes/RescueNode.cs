using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that catches exceptions and displays a fallback when errors occur.
/// Implements ILayoutProvider to ensure child content is clipped to bounds.
/// </summary>
public sealed class RescueNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The main child node (may throw during lifecycle methods).
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The fallback child node (shown when an error occurs).
    /// </summary>
    public Hex1bNode? FallbackChild { get; set; }

    /// <summary>
    /// The source widget for event args.
    /// </summary>
    public RescueWidget? SourceWidget { get; set; }

    /// <summary>
    /// Whether an error has occurred.
    /// </summary>
    public bool HasError { get; private set; }

    /// <summary>
    /// The exception that was caught, if any.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// The phase in which the error occurred.
    /// </summary>
    public RescueErrorPhase ErrorPhase { get; private set; }

    /// <summary>
    /// Whether to show detailed exception information.
    /// </summary>
    public bool ShowDetails { get; set; }

    /// <summary>
    /// Optional custom fallback widget builder.
    /// </summary>
    public Func<RescueContext, Hex1bWidget>? FallbackBuilder { get; set; }

    /// <summary>
    /// Handler called when an exception is caught.
    /// </summary>
    public Func<RescueEventArgs, Task>? RescueHandler { get; set; }

    /// <summary>
    /// Handler called after the rescue state is reset.
    /// </summary>
    public Func<RescueResetEventArgs, Task>? ResetHandler { get; set; }

    /// <summary>
    /// The focus ring for reconciling fallback children.
    /// Set during reconciliation so EnsureFallbackNode can use it.
    /// </summary>
    internal FocusRing? FocusRing { get; set; }

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for child content (our bounds).
    /// </summary>
    public Rect ClipRect => Bounds;
    
    /// <summary>
    /// The clip mode for the rescue node's content. Always clips to ensure
    /// fallback content doesn't overflow.
    /// </summary>
    public ClipMode ClipMode => ClipMode.Clip;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    /// <inheritdoc />
    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    /// <inheritdoc />
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    /// <summary>
    /// Gets the active child (either main child or fallback, depending on error state).
    /// </summary>
    private Hex1bNode? ActiveChild => HasError ? FallbackChild : Child;

    /// <summary>
    /// Captures an error and invokes the rescue handler.
    /// </summary>
    internal async Task CaptureErrorAsync(Exception ex, RescueErrorPhase phase)
    {
        HasError = true;
        Exception = ex;
        ErrorPhase = phase;

        if (RescueHandler != null && SourceWidget != null)
        {
            var args = new RescueEventArgs(SourceWidget, this, ex, phase);
            await RescueHandler(args);
        }
    }

    /// <summary>
    /// Resets the error state and invokes the reset handler.
    /// Called when the user triggers a retry.
    /// </summary>
    public async Task ResetAsync()
    {
        var previousException = Exception;
        var previousPhase = ErrorPhase;

        // Clear internal state first
        HasError = false;
        Exception = null;
        ErrorPhase = RescueErrorPhase.None;
        
        // Clear fallback child so it gets rebuilt if error occurs again
        FallbackChild = null;
        
        // Mark dirty to trigger re-render with normal child
        MarkDirty();

        // Then invoke the user's reset handler with the previous error info
        if (ResetHandler != null && SourceWidget != null && previousException != null)
        {
            var args = new RescueResetEventArgs(SourceWidget, this, previousException, previousPhase);
            await ResetHandler(args);
        }
    }

    /// <summary>
    /// Synchronous reset for use in button callbacks.
    /// </summary>
    public void Reset()
    {
        // Fire and forget - the async handler will complete eventually
        _ = ResetAsync();
    }

    /// <summary>
    /// Builds the fallback widget tree.
    /// </summary>
    internal Hex1bWidget BuildFallbackWidget()
    {
        var context = new RescueContext(Exception!, ErrorPhase, Reset);

        if (FallbackBuilder != null)
        {
            // User provided a custom fallback - wrap it in a theme panel
            return new ThemePanelWidget(BuildRescueThemeMutator(), FallbackBuilder(context));
        }

        // Build the default fallback UI
        return new ThemePanelWidget(BuildRescueThemeMutator(), BuildDefaultFallback(context));
    }

    /// <summary>
    /// Creates a theme mutator that applies rescue styling.
    /// </summary>
    private Func<Hex1bTheme, Hex1bTheme> BuildRescueThemeMutator()
    {
        return theme => theme
            // Global colors
            .Set(GlobalTheme.ForegroundColor, theme.Get(RescueTheme.ForegroundColor))
            .Set(GlobalTheme.BackgroundColor, theme.Get(RescueTheme.BackgroundColor))
            // Border styling
            .Set(BorderTheme.BorderColor, theme.Get(RescueTheme.BorderColor))
            .Set(BorderTheme.TitleColor, theme.Get(RescueTheme.TitleColor))
            .Set(BorderTheme.TopLeftCorner, theme.Get(RescueTheme.TopLeftCorner))
            .Set(BorderTheme.TopRightCorner, theme.Get(RescueTheme.TopRightCorner))
            .Set(BorderTheme.BottomLeftCorner, theme.Get(RescueTheme.BottomLeftCorner))
            .Set(BorderTheme.BottomRightCorner, theme.Get(RescueTheme.BottomRightCorner))
            .Set(BorderTheme.HorizontalLine, theme.Get(RescueTheme.HorizontalLine))
            .Set(BorderTheme.VerticalLine, theme.Get(RescueTheme.VerticalLine))
            // Separator styling
            .Set(SeparatorTheme.HorizontalChar, theme.Get(RescueTheme.SeparatorHorizontalChar))
            .Set(SeparatorTheme.VerticalChar, theme.Get(RescueTheme.SeparatorVerticalChar))
            .Set(SeparatorTheme.Color, theme.Get(RescueTheme.SeparatorColor))
            // Button styling
            .Set(ButtonTheme.ForegroundColor, theme.Get(RescueTheme.ButtonForegroundColor))
            .Set(ButtonTheme.BackgroundColor, theme.Get(RescueTheme.ButtonBackgroundColor))
            .Set(ButtonTheme.FocusedForegroundColor, theme.Get(RescueTheme.ButtonFocusedForegroundColor))
            .Set(ButtonTheme.FocusedBackgroundColor, theme.Get(RescueTheme.ButtonFocusedBackgroundColor));
    }

    /// <summary>
    /// Builds the default fallback widget tree.
    /// </summary>
    private Hex1bWidget BuildDefaultFallback(RescueContext ctx)
    {
        var title = ShowDetails ? "Exception Details" : "Error";
        
        return ctx.Border(b => [
            b.VStack(v => BuildFallbackContent(v, ctx)).Fill()
        ]).Title(title);
    }

    /// <summary>
    /// Builds the content for the fallback UI.
    /// </summary>
    private Hex1bWidget[] BuildFallbackContent(WidgetContext<VStackWidget> v, RescueContext ctx)
    {
        var widgets = new List<Hex1bWidget>();

        // Header
        widgets.Add(v.Text(ShowDetails ? "UNHANDLED EXCEPTION" : "APPLICATION ERROR"));

        if (ShowDetails && ctx.Exception != null)
        {
            widgets.Add(v.Text($"Phase: {ctx.ErrorPhase}"));
            widgets.Add(v.Text($"Type:  {ctx.Exception.GetType().FullName}").Ellipsis());
        }

        // Separator after header (uses theme for double-line style)
        widgets.Add(v.Separator());

        // Button row: Retry and Copy Details
        widgets.Add(v.HStack(h => [
            h.Button("Retry").OnClick(_ => ctx.Reset()),
            h.Text(" "), // Spacer
            h.Button("Copy Details").OnClick(e => e.Context.CopyToClipboard(FormatExceptionDetails(ctx)))
        ]));

        // Separator after button (uses theme for double-line style)
        widgets.Add(v.Separator());

        // Scrollable content area
        widgets.Add(v.VScrollPanel(content => BuildErrorContent(content, ctx)).Fill());

        return [.. widgets];
    }

    /// <summary>
    /// Formats the exception details as a plain text string for clipboard copying.
    /// </summary>
    private string FormatExceptionDetails(RescueContext ctx)
    {
        if (ctx.Exception == null)
        {
            return "No exception information available.";
        }

        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("=== EXCEPTION DETAILS ===");
        sb.AppendLine();
        sb.AppendLine($"Phase: {ctx.ErrorPhase}");
        sb.AppendLine($"Type:  {ctx.Exception.GetType().FullName}");
        sb.AppendLine();
        sb.AppendLine("Message:");
        sb.AppendLine(ctx.Exception.Message ?? "(no message)");
        
        if (ctx.Exception.StackTrace != null)
        {
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ctx.Exception.StackTrace);
        }
        
        if (ctx.Exception.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine($"=== INNER EXCEPTION: {ctx.Exception.InnerException.GetType().Name} ===");
            sb.AppendLine();
            sb.AppendLine("Message:");
            sb.AppendLine(ctx.Exception.InnerException.Message ?? "(no message)");
            
            if (ctx.Exception.InnerException.StackTrace != null)
            {
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ctx.Exception.InnerException.StackTrace);
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Builds the error details or friendly message content.
    /// </summary>
    private Hex1bWidget[] BuildErrorContent(WidgetContext<VStackWidget> v, RescueContext ctx)
    {
        if (ShowDetails && ctx.Exception != null)
        {
            return BuildDetailedErrorContent(v, ctx);
        }
        else
        {
            return BuildFriendlyErrorContent(v);
        }
    }

    /// <summary>
    /// Builds detailed error content with stack trace.
    /// </summary>
    private Hex1bWidget[] BuildDetailedErrorContent(WidgetContext<VStackWidget> v, RescueContext ctx)
    {
        var widgets = new List<Hex1bWidget>();

        // Message section
        widgets.Add(v.Text("Message:"));
        widgets.Add(v.Text(""));
        widgets.Add(v.Text("  " + (ctx.Exception!.Message ?? "(no message)")).Wrap());
        widgets.Add(v.Text(""));

        // Stack trace section
        if (ctx.Exception.StackTrace != null)
        {
            widgets.Add(v.Text("Stack Trace:"));
            widgets.Add(v.Text(""));

            foreach (var line in ctx.Exception.StackTrace.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    widgets.Add(v.Text("  " + trimmed).Wrap());
                }
            }
        }

        // Inner exception
        if (ctx.Exception.InnerException != null)
        {
            widgets.Add(v.Text(""));
            widgets.Add(v.Text($"Inner Exception: {ctx.Exception.InnerException.GetType().Name}"));
            widgets.Add(v.Text(""));
            widgets.Add(v.Text("  " + (ctx.Exception.InnerException.Message ?? "(no message)")).Wrap());

            if (ctx.Exception.InnerException.StackTrace != null)
            {
                widgets.Add(v.Text(""));
                widgets.Add(v.Text("Inner Stack Trace:"));
                widgets.Add(v.Text(""));

                foreach (var line in ctx.Exception.InnerException.StackTrace.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        widgets.Add(v.Text("  " + trimmed).Wrap());
                    }
                }
            }
        }

        return [.. widgets];
    }

    /// <summary>
    /// Builds friendly error content for production.
    /// </summary>
    private Hex1bWidget[] BuildFriendlyErrorContent(WidgetContext<VStackWidget> v)
    {
        return [
            v.Text(""),
            v.Text("Something went wrong."),
            v.Text(""),
            v.Text("The application encountered an unexpected error."),
            v.Text(""),
            v.Text("Please try again or contact support if the"),
            v.Text("problem persists."),
            v.Text(""),
            v.Text($"Error ID: {Guid.NewGuid():N}"[..36])
        ];
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (HasError)
        {
            return FallbackChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }

        try
        {
            return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }
        catch (Exception ex)
        {
            CaptureErrorAsync(ex, RescueErrorPhase.Measure).GetAwaiter().GetResult();
            EnsureFallbackNode();
            return FallbackChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        if (HasError)
        {
            FallbackChild?.Arrange(bounds);
            return;
        }

        try
        {
            Child?.Arrange(bounds);
        }
        catch (Exception ex)
        {
            CaptureErrorAsync(ex, RescueErrorPhase.Arrange).GetAwaiter().GetResult();
            EnsureFallbackNode();
            FallbackChild?.Arrange(bounds);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Set up clipping for rescue content
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        try
        {
            if (HasError)
            {
                // Use RenderChild for automatic caching support
                if (FallbackChild != null)
                {
                    context.RenderChild(FallbackChild);
                }
                return;
            }

            try
            {
                // Use RenderChild for automatic caching support
                if (Child != null)
                {
                    context.RenderChild(Child);
                }
            }
            catch (Exception ex)
            {
                CaptureErrorAsync(ex, RescueErrorPhase.Render).GetAwaiter().GetResult();
                EnsureFallbackNode();

                // Re-measure and arrange the fallback, then render it
                if (FallbackChild != null)
                {
                    FallbackChild.Measure(new Constraints(0, Bounds.Width, 0, Bounds.Height));
                    FallbackChild.Arrange(Bounds);
                    context.RenderChild(FallbackChild);
                }
            }
        }
        finally
        {
            context.CurrentLayoutProvider = previousLayout;
            ParentLayoutProvider = null;
        }
    }

    private void EnsureFallbackNode()
    {
        if (FallbackChild != null) return;

        // Build the fallback widget and create a node
        var fallbackWidget = BuildFallbackWidget();

        // Use the stored FocusRing so focusable nodes get registered properly
        var context = ReconcileContext.CreateRoot(FocusRing);
        FallbackChild = context.ReconcileChildAsync(null, fallbackWidget, this).GetAwaiter().GetResult();
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        var active = ActiveChild;
        if (active != null)
        {
            foreach (var focusable in active.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        var active = ActiveChild;
        if (active != null) yield return active;
    }
}
