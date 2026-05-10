using System.Threading;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record TextBoxWidget(string? Text = null) : Hex1bWidget,
    IStatefulWidget<TextBoxWidget, TextBoxState>
{
    /// <summary>Rebindable action: Move cursor left.</summary>
    public static readonly ActionId MoveLeft = new($"{nameof(TextBoxWidget)}.{nameof(MoveLeft)}");
    /// <summary>Rebindable action: Move cursor right.</summary>
    public static readonly ActionId MoveRight = new($"{nameof(TextBoxWidget)}.{nameof(MoveRight)}");
    /// <summary>Rebindable action: Move cursor to start.</summary>
    public static readonly ActionId MoveHome = new($"{nameof(TextBoxWidget)}.{nameof(MoveHome)}");
    /// <summary>Rebindable action: Move cursor to end.</summary>
    public static readonly ActionId MoveEnd = new($"{nameof(TextBoxWidget)}.{nameof(MoveEnd)}");
    /// <summary>Rebindable action: Move cursor to previous word.</summary>
    public static readonly ActionId MoveWordLeft = new($"{nameof(TextBoxWidget)}.{nameof(MoveWordLeft)}");
    /// <summary>Rebindable action: Move cursor to next word.</summary>
    public static readonly ActionId MoveWordRight = new($"{nameof(TextBoxWidget)}.{nameof(MoveWordRight)}");
    /// <summary>Rebindable action: Extend selection to previous word.</summary>
    public static readonly ActionId SelectWordLeft = new($"{nameof(TextBoxWidget)}.{nameof(SelectWordLeft)}");
    /// <summary>Rebindable action: Extend selection to next word.</summary>
    public static readonly ActionId SelectWordRight = new($"{nameof(TextBoxWidget)}.{nameof(SelectWordRight)}");
    /// <summary>Rebindable action: Extend selection left.</summary>
    public static readonly ActionId SelectLeft = new($"{nameof(TextBoxWidget)}.{nameof(SelectLeft)}");
    /// <summary>Rebindable action: Extend selection right.</summary>
    public static readonly ActionId SelectRight = new($"{nameof(TextBoxWidget)}.{nameof(SelectRight)}");
    /// <summary>Rebindable action: Select to start.</summary>
    public static readonly ActionId SelectToStart = new($"{nameof(TextBoxWidget)}.{nameof(SelectToStart)}");
    /// <summary>Rebindable action: Select to end.</summary>
    public static readonly ActionId SelectToEnd = new($"{nameof(TextBoxWidget)}.{nameof(SelectToEnd)}");
    /// <summary>Rebindable action: Delete character backward.</summary>
    public static readonly ActionId DeleteBackward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteBackward)}");
    /// <summary>Rebindable action: Delete character forward.</summary>
    public static readonly ActionId DeleteForward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteForward)}");
    /// <summary>Rebindable action: Delete previous word.</summary>
    public static readonly ActionId DeleteWordBackward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteWordBackward)}");
    /// <summary>Rebindable action: Delete next word.</summary>
    public static readonly ActionId DeleteWordForward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteWordForward)}");
    /// <summary>Rebindable action: Select all text.</summary>
    public static readonly ActionId SelectAll = new($"{nameof(TextBoxWidget)}.{nameof(SelectAll)}");
    /// <summary>Rebindable action: Submit text.</summary>
    public static readonly ActionId Submit = new($"{nameof(TextBoxWidget)}.{nameof(Submit)}");
    /// <summary>Rebindable action: Insert typed text.</summary>
    public static readonly ActionId InsertText = new($"{nameof(TextBoxWidget)}.{nameof(InsertText)}");
    /// <summary>Rebindable action: Move cursor up one line (multiline only).</summary>
    public static readonly ActionId MoveUp = new($"{nameof(TextBoxWidget)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move cursor down one line (multiline only).</summary>
    public static readonly ActionId MoveDown = new($"{nameof(TextBoxWidget)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Extend selection up one line (multiline only).</summary>
    public static readonly ActionId SelectUp = new($"{nameof(TextBoxWidget)}.{nameof(SelectUp)}");
    /// <summary>Rebindable action: Extend selection down one line (multiline only).</summary>
    public static readonly ActionId SelectDown = new($"{nameof(TextBoxWidget)}.{nameof(SelectDown)}");
    /// <summary>Rebindable action: Insert a newline (multiline only).</summary>
    public static readonly ActionId InsertNewline = new($"{nameof(TextBoxWidget)}.{nameof(InsertNewline)}");
    /// <summary>Rebindable action: Accept the inline prediction (default: Right Arrow at end of buffer).</summary>
    public static readonly ActionId AcceptPrediction = new($"{nameof(TextBoxWidget)}.{nameof(AcceptPrediction)}");
    /// <summary>Rebindable action: Dismiss the inline prediction (default: Escape while a prediction is showing).</summary>
    public static readonly ActionId DismissPrediction = new($"{nameof(TextBoxWidget)}.{nameof(DismissPrediction)}");

    /// <summary>
    /// Internal handler for text changed events.
    /// </summary>
    internal Func<TextChangedEventArgs, Task>? TextChangedHandler { get; init; }

    /// <summary>
    /// Internal handler for submit events.
    /// </summary>
    internal Func<TextSubmittedEventArgs, Task>? SubmitHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the text content changes.
    /// </summary>
    public TextBoxWidget OnTextChanged(Action<TextChangedEventArgs> handler)
        => this with { TextChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the text content changes.
    /// </summary>
    public TextBoxWidget OnTextChanged(Func<TextChangedEventArgs, Task> handler)
        => this with { TextChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when Enter is pressed in the text box.
    /// </summary>
    public TextBoxWidget OnSubmit(Action<TextSubmittedEventArgs> handler)
        => this with { SubmitHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when Enter is pressed in the text box.
    /// </summary>
    public TextBoxWidget OnSubmit(Func<TextSubmittedEventArgs, Task> handler)
        => this with { SubmitHandler = handler };

    /// <summary>
    /// Internal handler for paste events. When set, replaces the default paste behavior
    /// (which inserts text at cursor position).
    /// </summary>
    internal Func<PasteEventArgs, Task>? PasteHandler { get; init; }

    /// <summary>
    /// Sets an asynchronous handler called when paste data is received.
    /// Overrides the default behavior of inserting pasted text at the cursor position.
    /// </summary>
    public TextBoxWidget OnPaste(Func<PasteEventArgs, Task> handler)
        => this with { PasteHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when paste data is received.
    /// Overrides the default behavior of inserting pasted text at the cursor position.
    /// </summary>
    public TextBoxWidget OnPaste(Action<PasteEventArgs> handler)
        => this with { PasteHandler = e => { handler(e); return Task.CompletedTask; } };

    /// <summary>
    /// Minimum width of the text box in columns. When set, the text box will measure
    /// at least this many columns wide regardless of content.
    /// </summary>
    public int? MinWidth { get; init; }

    /// <summary>
    /// Maximum width of the text box in columns. When set, the text box will not exceed
    /// this width. Defaults to the same value as MinWidth if not explicitly set.
    /// </summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// When true, the text box supports multi-line editing.
    /// Enter inserts newlines, Up/Down navigate between lines.
    /// </summary>
    internal bool IsMultilineValue { get; init; }

    /// <summary>
    /// Maximum number of lines allowed in multiline mode.
    /// When null, there is no limit. Only applies when <see cref="IsMultilineValue"/> is true.
    /// </summary>
    internal int? MaxLinesValue { get; init; }

    /// <summary>
    /// When true, long lines are visually wrapped at word boundaries.
    /// Only applies when <see cref="IsMultilineValue"/> is true.
    /// </summary>
    internal bool IsWordWrapValue { get; init; }

    /// <summary>
    /// Fixed height in lines for the text box.
    /// When null, single-line uses 1, multiline sizes to content.
    /// </summary>
    internal int? HeightValue { get; init; }

    /// <summary>
    /// Enables multi-line text editing. Enter inserts newlines,
    /// Up/Down arrows navigate between lines, and word wrapping can be enabled.
    /// </summary>
    public TextBoxWidget Multiline()
        => this with { IsMultilineValue = true };

    /// <summary>
    /// Enables multi-line text editing with a maximum number of lines.
    /// Once the limit is reached, no more newlines can be inserted.
    /// </summary>
    public TextBoxWidget Multiline(int maxLines)
        => this with { IsMultilineValue = true, MaxLinesValue = maxLines };

    /// <summary>
    /// Enables word wrapping for multi-line text boxes. Long lines are
    /// visually broken at word boundaries to fit the available width.
    /// </summary>
    public TextBoxWidget WordWrap()
        => this with { IsWordWrapValue = true };

    /// <summary>
    /// Sets the height of the text box in lines.
    /// </summary>
    public TextBoxWidget Height(int lines)
        => this with { HeightValue = lines };

    /// <summary>
    /// Internal predictor callback. Invoked after each typed character (when
    /// the cursor is at the end of the buffer) with the current text, and
    /// expected to return the suggested completion text or <c>null</c> for
    /// no suggestion. The token is signalled when a newer keystroke arrives.
    /// </summary>
    internal Func<string, CancellationToken, Task<string?>>? PredictHandler { get; init; }

    /// <summary>
    /// Internal debounce window for <see cref="PredictHandler"/>. When greater
    /// than zero, the predictor is only invoked after the user pauses typing
    /// for this duration. <see cref="TimeSpan.Zero"/> (the default) invokes
    /// immediately.
    /// </summary>
    internal TimeSpan PredictionDebounceValue { get; init; }

    /// <summary>
    /// Configures an inline predictive-completion provider. After the user
    /// types a character (and only when the cursor is at the end of the
    /// buffer), <paramref name="predictor"/> is invoked with the current
    /// text. If it returns a non-null, non-empty string, that text is shown
    /// inline after the cursor in the prediction colors. The user accepts
    /// the suggestion with <see cref="AcceptPrediction"/> (Right Arrow by
    /// default) or dismisses it with <see cref="DismissPrediction"/>
    /// (Escape by default). The cancellation token is signalled when the
    /// user types another character before the predictor returns, so an
    /// expensive predictor can short-circuit.
    /// </summary>
    public TextBoxWidget Predict(Func<string, CancellationToken, Task<string?>> predictor)
        => this with { PredictHandler = predictor };

    /// <summary>
    /// Configures an inline predictive-completion provider with a debounce.
    /// The predictor is only invoked after the user pauses typing for
    /// <paramref name="debounce"/>; intermediate keystrokes restart the
    /// timer and cancel any in-flight request. Use this when the predictor
    /// is expensive (e.g. an LLM round-trip).
    /// </summary>
    public TextBoxWidget Predict(Func<string, CancellationToken, Task<string?>> predictor, TimeSpan debounce)
        => this with { PredictHandler = predictor, PredictionDebounceValue = debounce };

    /// <summary>
    /// Externally-supplied state instance. When set, the textbox becomes a
    /// pure view of <see cref="InjectedState"/> — the framework routes this
    /// exact instance into the underlying node on every reconcile, so the
    /// parent can mutate <c>state.Text</c> (and friends) directly and the
    /// change is reflected without any <see cref="OnTextChanged(System.Action{Events.TextChangedEventArgs})"/>
    /// shadow-sync handler. Pair with
    /// <see cref="Composition.CompositionContext.UseState{T}(System.Func{T})"/>
    /// inside a composite parent.
    /// </summary>
    internal TextBoxState? InjectedState { get; init; }

    /// <summary>
    /// Returns a copy of the textbox bound to the supplied <paramref name="state"/>
    /// instance. The textbox becomes a pure view of <paramref name="state"/>:
    /// every reconcile assigns this exact instance to the underlying node, and
    /// the framework no longer drift-detects against the constructor
    /// <see cref="Text"/> argument.
    /// </summary>
    /// <remarks>
    /// Supplying both a non-null constructor <see cref="Text"/> argument and
    /// <c>State(...)</c> is a programming error and will throw
    /// <see cref="System.InvalidOperationException"/> at reconcile time. Pick
    /// one source of truth.
    /// </remarks>
    public TextBoxWidget State(TextBoxState state)
        => this with { InjectedState = state };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBoxNode ?? new TextBoxNode();

        // Store reference to source widget for event args
        node.SourceWidget = this;

        if (InjectedState is not null)
        {
            // Hoisted-state path: the parent owns the TextBoxState instance and
            // is the single source of truth. We just route the instance into
            // the node on every reconcile and never drift-detect against a
            // constructor Text — that combination would be ambiguous and is
            // disallowed below.
            if (Text != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(TextBoxWidget)} cannot be constructed with both a Text " +
                    $"argument and {nameof(State)}(...). Pick one source of truth: " +
                    $"either pass an initial Text and let the framework manage the " +
                    $"state, or pass a TextBoxState owned by the parent (typically " +
                    $"via UseState&lt;TextBoxState&gt;()).");
            }

            node.State = InjectedState;
            // LastWidgetText is irrelevant in this mode; clear it so a future
            // switch back to the framework-managed mode doesn't see stale data.
            node.LastWidgetText = null;

            // The hoisted TextBoxState mutates outside the framework's dirty
            // tracking — e.g. when a composite's Build sets state.Text = ... or
            // when an OnSubmit handler clears the buffer. The state bumps an
            // internal Version on every such mutation; we mark the node dirty
            // when the version advances so the new value reaches the screen on
            // the next render.
            if (InjectedState.Version != node.LastSeenStateVersion)
            {
                node.LastSeenStateVersion = InjectedState.Version;
                node.MarkDirty();
            }
        }
        else
        {
            // Framework-managed path: the node owns its TextBoxState. Seed on
            // first reconcile, and observe a changed widget Text on subsequent
            // reconciles so callers that rebuild the widget with a fresh Text
            // (e.g. a redux-style render) still see the new value.
            if (context.IsNew && Text != null)
            {
                var oldText = node.Text;
                node.Text = Text;
                node.LastWidgetText = Text;
                if (oldText != node.Text)
                {
                    node.State.ClearSelection();
                    node.State.CursorPosition = node.Text.Length;
                }
            }
            else if (!context.IsNew && Text != null && Text != node.LastWidgetText)
            {
                // External code changed the text value in the widget - update node.
                var oldText = node.Text;
                node.Text = Text;
                node.LastWidgetText = Text;
                if (oldText != node.Text)
                {
                    node.State.ClearSelection();
                    node.State.CursorPosition = node.Text.Length;
                }
            }
        }

        // Set up event handlers - wrap to convert InputBindingActionContext to typed event args
        if (TextChangedHandler != null)
        {
            node.TextChangedAction = (ctx, oldText, newText) =>
            {
                var args = new TextChangedEventArgs(this, node, ctx, oldText, newText);
                return TextChangedHandler(args);
            };
        }
        else
        {
            node.TextChangedAction = null;
        }

        if (SubmitHandler != null)
        {
            node.SubmitAction = ctx =>
            {
                var args = new TextSubmittedEventArgs(this, node, ctx, node.Text);
                return SubmitHandler(args);
            };
        }
        else
        {
            node.SubmitAction = null;
        }

        // Wire paste handler
        node.CustomPasteAction = PasteHandler;

        // Sync min/max width to node
        node.MinWidth = MinWidth;
        node.MaxWidth = MaxWidth ?? MinWidth;

        // Sync multiline properties
        node.IsMultiline = IsMultilineValue;
        node.IsWordWrap = IsWordWrapValue;
        node.RequestedHeight = HeightValue;
        node.MaxLines = MaxLinesValue;
        node.State.IsMultiline = IsMultilineValue;
        node.State.MaxLines = MaxLinesValue;

        // Sync predictor configuration. Removing the handler also clears any
        // in-flight prediction so the overlay disappears on the next render.
        var predictorChanged = !ReferenceEquals(node.Predictor, PredictHandler);
        node.Predictor = PredictHandler;
        node.PredictionDebounce = PredictionDebounceValue;
        if (predictorChanged && PredictHandler is null)
        {
            node.ClearPrediction();
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TextBoxNode);
}
