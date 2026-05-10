using System.Threading;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public TextBoxWidget? SourceWidget { get; set; }

    /// <summary>
    /// The text box state containing text, cursor position, and selection.
    /// </summary>
    internal TextBoxState State { get; set; } = new();

    /// <summary>
    /// Tracks the last text value provided by the widget during reconciliation.
    /// Used to detect when external code changes the text vs internal user input.
    /// </summary>
    internal string? LastWidgetText { get; set; }

    /// <summary>
    /// Tracks the last <see cref="TextBoxState.Version"/> seen by reconcile when
    /// the node is bound to an externally-owned (hoisted) state instance. When
    /// the parent mutates <see cref="TextBoxState.Text"/> or <c>CursorPosition</c>
    /// directly (e.g. inside a composite's <c>Build</c>), the state's version
    /// counter advances; reconcile compares it against this field and marks the
    /// node dirty so the change is visible on the next render.
    /// </summary>
    internal long LastSeenStateVersion { get; set; }
    
    /// <summary>
    /// Gets or sets the text content. Convenience property that accesses State.Text.
    /// </summary>
    public string Text
    {
        get => State.Text;
        set
        {
            if (State.Text != value)
            {
                State.Text = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Internal action invoked when text content changes.
    /// </summary>
    internal Func<InputBindingActionContext, string, string, Task>? TextChangedAction { get; set; }

    /// <summary>
    /// Internal action invoked when Enter is pressed.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SubmitAction { get; set; }

    /// <summary>
    /// Custom paste handler that overrides the default text insertion behavior.
    /// When null, paste is handled by inserting text at cursor position.
    /// </summary>
    internal Func<Events.PasteEventArgs, Task>? CustomPasteAction { get; set; }

    /// <summary>
    /// Minimum width of the text box in columns.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Maximum width of the text box in columns.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// The character index of the first visible character in the viewport.
    /// Adjusted automatically to keep the cursor visible.
    /// </summary>
    internal int ScrollOffset { get; set; }

    /// <summary>
    /// When true, the text box supports multi-line editing.
    /// </summary>
    public bool IsMultiline { get; set; }

    /// <summary>
    /// Maximum number of lines allowed in multiline mode.
    /// When null, there is no limit.
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// When true, long lines are visually wrapped at word boundaries.
    /// </summary>
    public bool IsWordWrap { get; set; }

    /// <summary>
    /// Fixed height in lines. Null means 1 for single-line, content-based for multiline.
    /// </summary>
    public int? RequestedHeight { get; set; }

    /// <summary>
    /// The 0-based index of the first visible line for vertical scrolling.
    /// </summary>
    internal int VerticalScrollOffset { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                if (!value)
                {
                    // Lose focus → drop any active suggestion so it doesn't
                    // ghost into the next focused widget's render area.
                    ClearPrediction();
                }
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    /// <summary>
    /// Mouse hover uses the default cursor shape. The text editing bar cursor
    /// is shown via the native terminal cursor only when the TextBox is focused.
    /// </summary>
    public override CursorShape PreferredCursorShape => CursorShape.Default;

    /// <summary>
    /// Screen X coordinate where the native terminal cursor should be placed.
    /// Set during Render; -1 means no native cursor requested (e.g., during selection).
    /// </summary>
    internal int ScreenCursorX { get; private set; } = -1;

    /// <summary>
    /// Screen Y coordinate where the native terminal cursor should be placed.
    /// </summary>
    internal int ScreenCursorY { get; private set; }

    // ───── Predictive input ────────────────────────────────────────────────

    /// <summary>
    /// User-supplied async predictor. When non-null, typing a character with
    /// the cursor at end of buffer asks the predictor for an inline suggestion.
    /// </summary>
    internal Func<string, CancellationToken, Task<string?>>? Predictor { get; set; }

    /// <summary>
    /// Optional debounce window. When &gt; <see cref="TimeSpan.Zero"/>, multiple
    /// keystrokes within the window collapse into a single predictor invocation.
    /// </summary>
    internal TimeSpan PredictionDebounce { get; set; }

    /// <summary>
    /// The current inline prediction string shown after the cursor.
    /// <c>null</c> when no prediction is active. Always cleared on any
    /// non-typing input (movement, deletion, paste, focus loss, mouse click).
    /// </summary>
    internal string? CurrentPrediction { get; private set; }

    /// <summary>
    /// Monotonic counter incremented every time a prediction request is issued
    /// or invalidated. Async predictor callbacks compare the captured id to
    /// this field on completion and discard stale results.
    /// </summary>
    private long _predictionRequestId;

    /// <summary>
    /// Cancels the in-flight predictor task. Replaced atomically each time a
    /// new prediction is requested or an invalidation occurs.
    /// </summary>
    private CancellationTokenSource? _predictionInflightCts;

    /// <summary>
    /// Cancels the pending debounce delay for the next prediction request.
    /// </summary>
    private CancellationTokenSource? _predictionDebounceCts;

    /// <summary>
    /// Cancels any in-flight prediction work and clears
    /// <see cref="CurrentPrediction"/>. Returns <c>true</c> if a prediction
    /// was actually showing (used by Escape to decide whether to consume).
    /// </summary>
    internal bool ClearPrediction()
    {
        // Always invalidate request id so any racing predictor task discards
        // its result, even if no prediction is currently displayed.
        _predictionRequestId++;

        try { _predictionDebounceCts?.Cancel(); } catch { }
        _predictionDebounceCts?.Dispose();
        _predictionDebounceCts = null;

        try { _predictionInflightCts?.Cancel(); } catch { }
        _predictionInflightCts?.Dispose();
        _predictionInflightCts = null;

        if (CurrentPrediction is null)
            return false;

        CurrentPrediction = null;
        MarkDirty();
        return true;
    }

    /// <summary>
    /// Replaces the current prediction string and marks the node dirty.
    /// </summary>
    private void SetPrediction(string? prediction)
    {
        if (CurrentPrediction == prediction)
            return;
        CurrentPrediction = string.IsNullOrEmpty(prediction) ? null : prediction;
        MarkDirty();
    }

    /// <summary>
    /// Returns true when a prediction is currently shown and should respond
    /// to <c>RightArrow</c> (accept) and <c>Escape</c> (dismiss).
    /// </summary>
    private bool HasActivePrediction => !string.IsNullOrEmpty(CurrentPrediction)
        && IsFocused
        && State.CursorPosition == State.Text.Length
        && !State.HasSelection;

    /// <summary>
    /// Inserts the current prediction at the end of the buffer (accepting it),
    /// fires <see cref="TextChangedAction"/>, and clears prediction state.
    /// Returns the new text on success; <c>null</c> if no prediction was active.
    /// </summary>
    private async Task<string?> AcceptCurrentPredictionAsync(InputBindingActionContext ctx)
    {
        var prediction = CurrentPrediction;
        if (string.IsNullOrEmpty(prediction))
            return null;

        var oldText = State.Text;
        // Append at the very end — by precondition the cursor is at end and
        // there is no selection.
        State.Text = oldText + prediction;
        State.CursorPosition = State.Text.Length;
        State.ResetPreferredColumn();

        // Clear before invoking the change handler so handlers observing the
        // node see a coherent post-accept state.
        ClearPrediction();
        MarkDirty();

        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }

        return State.Text;
    }

    /// <summary>
    /// Requests a new prediction for the current buffer. Honours the
    /// configured debounce window and uses request-id + cancellation to
    /// discard stale results when the user keeps typing.
    /// </summary>
    private void RequestPrediction()
    {
        var predictor = Predictor;
        if (predictor is null) return;
        // Predictions are a single-line affordance only.
        if (IsMultiline) return;
        if (!IsFocused) return;
        if (State.HasSelection) return;
        if (State.CursorPosition != State.Text.Length) return;

        // Cancel pending/in-flight work and bump the request id.
        var requestId = ++_predictionRequestId;
        try { _predictionDebounceCts?.Cancel(); } catch { }
        _predictionDebounceCts?.Dispose();
        try { _predictionInflightCts?.Cancel(); } catch { }
        _predictionInflightCts?.Dispose();

        var snapshot = State.Text;
        var debounce = PredictionDebounce;
        var inflight = new CancellationTokenSource();
        _predictionInflightCts = inflight;
        var debounceCts = debounce > TimeSpan.Zero ? new CancellationTokenSource() : null;
        _predictionDebounceCts = debounceCts;

        _ = Task.Run(async () =>
        {
            try
            {
                if (debounceCts is not null)
                {
                    await Task.Delay(debounce, debounceCts.Token);
                }

                if (inflight.IsCancellationRequested) return;

                var result = await predictor(snapshot, inflight.Token);

                // Drop result if a newer request superseded us, the input was
                // mutated, the cursor moved off the end, or focus was lost.
                if (requestId != _predictionRequestId) return;
                if (inflight.IsCancellationRequested) return;
                if (!IsFocused) return;
                if (State.HasSelection) return;
                if (State.Text != snapshot) return;
                if (State.CursorPosition != State.Text.Length) return;
                if (string.IsNullOrEmpty(result)) return;

                SetPrediction(result);
                AppInvalidate?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Stale request — ignore.
            }
            catch
            {
                // Predictor threw — silently swallow so a misbehaving predictor
                // can't crash the render loop.
            }
        });
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation
        bindings.Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.MoveLeft, MoveLeft, "Move left");
        bindings.Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.MoveRight, MoveRightOrAcceptPredictionAsync, "Move right / accept prediction");
        bindings.Key(Hex1bKey.Home).Triggers(TextBoxWidget.MoveHome, MoveHome, "Go to start");
        bindings.Key(Hex1bKey.End).Triggers(TextBoxWidget.MoveEnd, MoveEnd, "Go to end");
        
        // Word navigation (Ctrl+Arrow)
        bindings.Ctrl().Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.MoveWordLeft, MoveWordLeft, "Move to previous word");
        bindings.Ctrl().Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.MoveWordRight, MoveWordRight, "Move to next word");
        
        // Selection navigation
        bindings.Shift().Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.SelectLeft, SelectLeft, "Extend selection left");
        bindings.Shift().Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.SelectRight, SelectRight, "Extend selection right");
        bindings.Shift().Key(Hex1bKey.Home).Triggers(TextBoxWidget.SelectToStart, SelectToStart, "Select to start");
        bindings.Shift().Key(Hex1bKey.End).Triggers(TextBoxWidget.SelectToEnd, SelectToEnd, "Select to end");

        // Word selection (Ctrl+Shift+Arrow)
        bindings.Ctrl().Shift().Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.SelectWordLeft, SelectWordLeft, "Select to previous word");
        bindings.Ctrl().Shift().Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.SelectWordRight, SelectWordRight, "Select to next word");
        
        // Editing - use async handlers to fire callbacks
        bindings.Key(Hex1bKey.Backspace).Triggers(TextBoxWidget.DeleteBackward, DeleteBackwardAsync, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Triggers(TextBoxWidget.DeleteForward, DeleteForwardAsync, "Delete forward");
        
        // Word deletion (Ctrl+Backspace/Delete)
        bindings.Ctrl().Key(Hex1bKey.Backspace).Triggers(TextBoxWidget.DeleteWordBackward, DeleteWordBackwardAsync, "Delete previous word");
        bindings.Ctrl().Key(Hex1bKey.Delete).Triggers(TextBoxWidget.DeleteWordForward, DeleteWordForwardAsync, "Delete next word");

        // Multiline navigation and Enter handling
        if (IsMultiline)
        {
            bindings.Key(Hex1bKey.UpArrow).Triggers(TextBoxWidget.MoveUp, MoveUp, "Move up");
            bindings.Key(Hex1bKey.DownArrow).Triggers(TextBoxWidget.MoveDown, MoveDownAsync, "Move down");
            bindings.Shift().Key(Hex1bKey.UpArrow).Triggers(TextBoxWidget.SelectUp, SelectUp, "Extend selection up");
            bindings.Shift().Key(Hex1bKey.DownArrow).Triggers(TextBoxWidget.SelectDown, SelectDown, "Extend selection down");
            bindings.Key(Hex1bKey.Enter).Triggers(TextBoxWidget.InsertNewline, InsertNewlineAsync, "Insert newline");
        }
        else if (SubmitAction != null)
        {
            // Submit (Enter key) — single-line only
            bindings.Key(Hex1bKey.Enter).Triggers(TextBoxWidget.Submit, SubmitAction, "Submit");
        }

        // Escape only consumes when there is an active prediction. Bindings
        // are rebuilt per key event, so registering this conditionally lets
        // a "naked" Escape bubble to ancestors (form / window) as before.
        if (HasActivePrediction)
        {
            bindings.Key(Hex1bKey.Escape).Triggers(TextBoxWidget.DismissPrediction, DismissPredictionAction, "Dismiss prediction");
        }

        // Selection
        bindings.Ctrl().Key(Hex1bKey.A).Triggers(TextBoxWidget.SelectAll, SelectAll, "Select all");
        
        // Mouse: double-click to select all
        bindings.Mouse(MouseButton.Left).DoubleClick().Triggers(TextBoxWidget.SelectAll, SelectAll, "Select all");
        
        // Character input - matches any printable text (including emojis)
        bindings.AnyCharacter().Action(InsertTextAsync, "Type text");
    }

    /// <summary>
    /// RightArrow either moves the cursor right (default) or accepts the
    /// active prediction and inserts it into the buffer.
    /// </summary>
    private async Task MoveRightOrAcceptPredictionAsync(InputBindingActionContext ctx)
    {
        if (HasActivePrediction)
        {
            await AcceptCurrentPredictionAsync(ctx);
            return;
        }
        MoveRight();
    }

    /// <summary>
    /// Escape handler registered only while a prediction is showing —
    /// dismisses the suggestion. (When no prediction is active this binding
    /// is not registered, so Escape bubbles normally.)
    /// </summary>
    private void DismissPredictionAction()
    {
        ClearPrediction();
    }

    /// <summary>
    /// Handles bracketed paste. If a custom paste handler is set via OnPaste(),
    /// it is called instead of the default text insertion behavior.
    /// For default behavior: reads full paste content and inserts at cursor position.
    /// For single-line text boxes, newlines are replaced with spaces.
    /// </summary>
    public override async Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
    {
        // Paste is treated as an out-of-band edit — drop any in-flight prediction.
        ClearPrediction();

        // Custom handler overrides default behavior
        if (CustomPasteAction != null)
        {
            await CustomPasteAction(new Events.PasteEventArgs(pasteEvent.Paste));
            return InputResult.Handled;
        }

        var pastedText = await pasteEvent.Paste.ReadToEndAsync();

        if (string.IsNullOrEmpty(pastedText))
            return InputResult.Handled;

        // Single-line text box: replace newlines with spaces
        // Multiline: normalize line endings to \n but preserve newlines
        pastedText = pastedText.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!IsMultiline)
        {
            pastedText = pastedText.Replace("\n", " ");
        }

        var oldText = State.Text;

        if (State.HasSelection)
        {
            DeleteSelection();
        }

        State.Text = State.Text.Insert(State.CursorPosition, pastedText);
        State.CursorPosition += pastedText.Length;
        State.ResetPreferredColumn();
        MarkDirty();

        if (TextChangedAction != null && oldText != State.Text)
        {
            // Create a minimal context for the paste callback
            var ctx = new InputBindingActionContext(null!, null, default);
            await TextChangedAction(ctx, oldText, State.Text);
        }

        return InputResult.Handled;
    }

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        // If there's a selection, delete it first
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        State.Text = State.Text.Insert(State.CursorPosition, text);
        State.CursorPosition += text.Length;
        State.ResetPreferredColumn();

        // Drop any stale prediction — the buffer just changed.
        ClearPrediction();
        MarkDirty();

        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }

        // Predictions only fire on real keystrokes (this method) and only
        // when the cursor is at the very end of the buffer.
        RequestPrediction();
    }

    private void MoveLeft()
    {
        ClearPrediction();
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionStart;
            State.ClearSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveRight()
    {
        ClearPrediction();
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionEnd;
            State.ClearSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveHome()
    {
        ClearPrediction();
        State.ClearSelection();
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line);
        }
        else
        {
            State.CursorPosition = 0;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveEnd()
    {
        ClearPrediction();
        State.ClearSelection();
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
        }
        else
        {
            State.CursorPosition = State.Text.Length;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveWordLeft()
    {
        ClearPrediction();
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionStart;
            State.ClearSelection();
        }
        State.CursorPosition = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveWordRight()
    {
        ClearPrediction();
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionEnd;
            State.ClearSelection();
        }
        State.CursorPosition = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectLeft()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectRight()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectToStart()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line);
        }
        else
        {
            State.CursorPosition = 0;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectToEnd()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
        }
        else
        {
            State.CursorPosition = State.Text.Length;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectWordLeft()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectWordRight()
    {
        ClearPrediction();
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveUp()
    {
        ClearPrediction();
        State.MoveUp();
        MarkDirty();
    }

    private async Task MoveDownAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
        if (line >= State.GetLineCount() - 1)
        {
            // No line below — move to end of current line and insert newline
            if (!CanInsertNewline())
                return;

            var oldText = State.Text;
            // Move cursor to end of current line
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
            State.ClearSelection();
            State.InsertNewline();
            MarkDirty();

            if (TextChangedAction != null && oldText != State.Text)
                await TextChangedAction(ctx, oldText, State.Text);
        }
        else
        {
            State.MoveDown();
            MarkDirty();
        }
    }

    private void SelectUp()
    {
        ClearPrediction();
        State.MoveUp(extend: true);
        MarkDirty();
    }

    private void SelectDown()
    {
        ClearPrediction();
        State.MoveDown(extend: true);
        MarkDirty();
    }

    private bool CanInsertNewline()
        => MaxLines is null || State.GetLineCount() < MaxLines.Value;

    private async Task InsertNewlineAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        if (!CanInsertNewline())
            return;

        var oldText = State.Text;
        State.InsertNewline();
        State.ResetPreferredColumn();
        MarkDirty();

        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteBackwardAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterStart = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = State.CursorPosition - clusterStart;
            State.Text = State.Text.Remove(clusterStart, clusterLength);
            State.CursorPosition = clusterStart;
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteForwardAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterEnd = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = clusterEnd - State.CursorPosition;
            State.Text = State.Text.Remove(State.CursorPosition, clusterLength);
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteWordBackwardAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition > 0)
        {
            var wordStart = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
            var deleteLength = State.CursorPosition - wordStart;
            State.Text = State.Text.Remove(wordStart, deleteLength);
            State.CursorPosition = wordStart;
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteWordForwardAsync(InputBindingActionContext ctx)
    {
        ClearPrediction();
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            var wordEnd = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
            var deleteLength = wordEnd - State.CursorPosition;
            State.Text = State.Text.Remove(State.CursorPosition, deleteLength);
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private void DeleteSelection()
    {
        if (!State.HasSelection) return;
        var start = State.SelectionStart;
        var end = State.SelectionEnd;
        State.Text = State.Text[..start] + State.Text[end..];
        State.CursorPosition = start;
        State.ClearSelection();
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectAll()
    {
        ClearPrediction();
        State.SelectAll();
        MarkDirty();
    }

    /// <summary>
    /// Handles mouse click to position the cursor within the text.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // Only handle left clicks that aren't double/triple clicks
        if (mouseEvent.Button != MouseButton.Left || mouseEvent.ClickCount > 1)
        {
            return InputResult.NotHandled;
        }

        // Any click cancels an active prediction.
        ClearPrediction();

        if (IsMultiline)
        {
            return HandleMouseClickMultiline(localX, localY);
        }

        // Single-line: localX maps directly to a column in the visible text.
        var visStart = Math.Min(ScrollOffset, State.Text.Length);
        var visEnd = Math.Min(visStart + Math.Max(0, Bounds.Width), State.Text.Length);
        var visibleText = State.Text[visStart..visEnd];
        var visPos = DisplayColumnToVisibleTextPosition(visibleText, Math.Max(0, localX));
        State.ClearSelection();
        State.CursorPosition = visStart + visPos;

        return InputResult.Handled;
    }

    private InputResult HandleMouseClickMultiline(int localX, int localY)
    {
        var viewportWidth = Bounds.Width;
        var visualLines = BuildVisualLines(viewportWidth);

        var clickedVisualLine = VerticalScrollOffset + localY;
        if (clickedVisualLine >= visualLines.Count)
        {
            // Clicked below content — position at end
            State.ClearSelection();
            State.CursorPosition = State.Text.Length;
        }
        else
        {
            var vLine = visualLines[clickedVisualLine];
            var visPos = DisplayColumnToVisibleTextPosition(vLine.Text, localX);
            State.ClearSelection();
            State.CursorPosition = vLine.DocStartOffset + visPos;
        }

        MarkDirty();
        return InputResult.Handled;
    }

    /// <summary>
    /// Converts a display column (0-based, relative to text start) to a text cursor position.
    /// Accounts for variable-width characters (wide chars, emoji, etc.).
    /// </summary>
    private int DisplayColumnToTextPosition(int displayColumn)
    {
        if (string.IsNullOrEmpty(State.Text))
            return 0;

        int currentColumn = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(State.Text);
        int lastPosition = 0;

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            var graphemeStart = enumerator.ElementIndex;

            // If the click is before or at the start of this grapheme
            if (displayColumn < currentColumn + graphemeWidth)
            {
                // Click is in the first half of the grapheme - position before it
                // Click is in the second half - position after it
                var midpoint = currentColumn + (graphemeWidth / 2.0);
                if (displayColumn < midpoint)
                {
                    return graphemeStart;
                }
                else
                {
                    return graphemeStart + grapheme.Length;
                }
            }

            currentColumn += graphemeWidth;
            lastPosition = graphemeStart + grapheme.Length;
        }

        // Click is past the end of the text
        return State.Text.Length;
    }

    /// <summary>
    /// Maps a display column to a character position within a visible text string.
    /// Used for hover cursor positioning when text is scrolled.
    /// </summary>
    private static int DisplayColumnToVisibleTextPosition(string visibleText, int displayColumn)
    {
        if (string.IsNullOrEmpty(visibleText))
            return 0;

        int currentColumn = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(visibleText);
        int lastPosition = 0;

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            var graphemeStart = enumerator.ElementIndex;

            if (displayColumn < currentColumn + graphemeWidth)
            {
                var midpoint = currentColumn + (graphemeWidth / 2.0);
                return displayColumn < midpoint ? graphemeStart : graphemeStart + grapheme.Length;
            }

            currentColumn += graphemeWidth;
            lastPosition = graphemeStart + grapheme.Length;
        }

        return visibleText.Length;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (IsMultiline)
        {
            return MeasureMultiline(constraints);
        }

        var textDisplayWidth = Math.Max(DisplayWidth.GetStringWidth(State.Text), 1);

        int width;
        if (MinWidth.HasValue || MaxWidth.HasValue)
        {
            width = MinWidth.HasValue ? Math.Max(textDisplayWidth, MinWidth.Value) : textDisplayWidth;
            if (MaxWidth.HasValue)
                width = Math.Min(width, MaxWidth.Value);
        }
        else
        {
            // Inline fill mode: width is just the text content. The empty-text
            // floor of 1 above keeps the cursor cell visible.
            width = textDisplayWidth;
        }

        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    private Size MeasureMultiline(Constraints constraints)
    {
        // Width: fill available space, or use min/max width
        int width;
        if (MinWidth.HasValue || MaxWidth.HasValue)
        {
            width = MinWidth.HasValue ? MinWidth.Value : 1;
            if (MaxWidth.HasValue)
                width = Math.Min(width, MaxWidth.Value);
        }
        else
        {
            // Fill available width
            width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 40;
        }

        // Height: use requested height, or size to content
        int height;
        if (RequestedHeight.HasValue)
        {
            height = RequestedHeight.Value;
        }
        else
        {
            // Size to content — count visual lines (with word wrap if enabled)
            var viewportWidth = width; // fill mode in multiline
            height = IsWordWrap
                ? ComputeWrappedLineCount(viewportWidth)
                : State.GetLineCount();
            height = Math.Max(1, height);
        }

        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Adjusts <see cref="ScrollOffset"/> so the cursor position stays within the visible viewport.
    /// </summary>
    private void EnsureCursorInViewport(int viewportWidth)
    {
        var cursor = State.CursorPosition;
        var text = State.Text;

        // Clamp scroll offset to valid range
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, text.Length));

        // If cursor is before the viewport, scroll left
        if (cursor < ScrollOffset)
        {
            ScrollOffset = cursor;
        }

        // If cursor is at or past the right edge, scroll right.
        // We need at least 1 column for the cursor character (or the end-of-text cursor space).
        if (viewportWidth > 0 && cursor >= ScrollOffset + viewportWidth)
        {
            ScrollOffset = cursor - viewportWidth + 1;
        }

        // Final clamp
        ScrollOffset = Math.Max(0, ScrollOffset);
    }

    /// <summary>
    /// Extracts the visible portion of text for the current viewport, returning
    /// the visible string and the cursor position relative to the visible string.
    /// </summary>
    private (string visibleText, int visibleCursor, int visibleSelStart, int visibleSelEnd)
        GetViewportSlice(int viewportWidth)
    {
        var text = State.Text;
        var cursor = State.CursorPosition;

        // Calculate how many chars fit in the viewport starting from ScrollOffset
        var visStart = Math.Min(ScrollOffset, text.Length);
        var visEnd = Math.Min(visStart + viewportWidth, text.Length);
        var visibleText = text[visStart..visEnd];
        var visibleCursor = cursor - visStart;

        // Clamp selection to visible range
        var visSelStart = 0;
        var visSelEnd = 0;
        if (State.HasSelection)
        {
            visSelStart = Math.Clamp(State.SelectionStart - visStart, 0, visibleText.Length);
            visSelEnd = Math.Clamp(State.SelectionEnd - visStart, 0, visibleText.Length);
        }

        return (visibleText, visibleCursor, visSelStart, visSelEnd);
    }

    #region Multiline helpers

    /// <summary>
    /// Represents one visual line to be rendered, potentially a wrapped segment of a document line.
    /// </summary>
    private readonly record struct VisualLine(int DocLine, string Text, int DocStartOffset, bool IsContinuation);

    /// <summary>
    /// Computes the total number of visual lines after word wrapping.
    /// </summary>
    private int ComputeWrappedLineCount(int viewportWidth)
    {
        if (viewportWidth <= 0) return State.GetLineCount();
        var count = 0;
        var lineCount = State.GetLineCount();
        for (var i = 0; i < lineCount; i++)
        {
            var lineText = State.GetLineText(i);
            count += WrapLine(lineText, viewportWidth).Count;
        }
        return Math.Max(1, count);
    }

    /// <summary>
    /// Wraps a single line of text into segments that fit within viewportWidth.
    /// Returns a list of (segment text, start column in original line).
    /// </summary>
    private static List<(string text, int startCol)> WrapLine(string line, int viewportWidth)
    {
        if (viewportWidth <= 0 || line.Length <= viewportWidth)
            return [(line, 0)];

        var segments = new List<(string text, int startCol)>();
        var offset = 0;

        while (offset < line.Length)
        {
            var remaining = line.Length - offset;
            if (remaining <= viewportWidth)
            {
                segments.Add((line[offset..], offset));
                break;
            }

            // Try to break at a word boundary (space) within the viewport width
            var breakPoint = line.LastIndexOf(' ', offset + viewportWidth - 1, viewportWidth);
            if (breakPoint <= offset)
            {
                // No word boundary found — hard break
                breakPoint = offset + viewportWidth;
            }
            else
            {
                breakPoint++; // Include the space in this segment
            }

            segments.Add((line[offset..breakPoint], offset));
            offset = breakPoint;
        }

        if (segments.Count == 0)
            segments.Add(("", 0));

        return segments;
    }

    /// <summary>
    /// Builds the list of visual lines for the entire document, applying word wrap if enabled.
    /// </summary>
    private List<VisualLine> BuildVisualLines(int viewportWidth)
    {
        var result = new List<VisualLine>();
        var lineCount = State.GetLineCount();

        for (var docLine = 0; docLine < lineCount; docLine++)
        {
            var lineText = State.GetLineText(docLine);
            var lineStartOffset = State.GetLineStartOffset(docLine);

            if (IsWordWrap && viewportWidth > 0 && lineText.Length > viewportWidth)
            {
                var segments = WrapLine(lineText, viewportWidth);
                for (var i = 0; i < segments.Count; i++)
                {
                    result.Add(new VisualLine(docLine, segments[i].text, lineStartOffset + segments[i].startCol, i > 0));
                }
            }
            else
            {
                result.Add(new VisualLine(docLine, lineText, lineStartOffset, false));
            }
        }

        if (result.Count == 0)
            result.Add(new VisualLine(0, "", 0, false));

        return result;
    }

    /// <summary>
    /// Finds which visual line index contains the given document offset.
    /// </summary>
    private static int FindVisualLineForOffset(List<VisualLine> visualLines, int offset)
    {
        for (var i = visualLines.Count - 1; i >= 0; i--)
        {
            if (offset >= visualLines[i].DocStartOffset)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Ensures the cursor's visual line is visible within the vertical viewport.
    /// </summary>
    private void EnsureCursorLineInViewport(int visibleLines, List<VisualLine> visualLines)
    {
        var cursorVisualLine = FindVisualLineForOffset(visualLines, State.CursorPosition);
        VerticalScrollOffset = Math.Clamp(VerticalScrollOffset, 0, Math.Max(0, visualLines.Count - 1));

        if (cursorVisualLine < VerticalScrollOffset)
        {
            VerticalScrollOffset = cursorVisualLine;
        }
        else if (cursorVisualLine >= VerticalScrollOffset + visibleLines)
        {
            VerticalScrollOffset = cursorVisualLine - visibleLines + 1;
        }
    }

    /// <summary>
    /// Renders a multiline text box with vertical scrolling and per-line horizontal viewport.
    /// </summary>
    private void RenderMultiline(Hex1bRenderContext context, string globalColors, string resetToGlobal,
        Hex1bColor fillBg, Hex1bColor cursorFg, Hex1bColor cursorBg,
        Hex1bColor selFg, Hex1bColor selBg)
    {
        var viewportWidth = Bounds.Width;
        var viewportHeight = Bounds.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        var fillBgAnsi = fillBg.ToBackgroundAnsi();
        var visualLines = BuildVisualLines(viewportWidth);

        // Ensure cursor is vertically visible
        EnsureCursorLineInViewport(viewportHeight, visualLines);

        var cursor = State.CursorPosition;
        var selStart = State.HasSelection ? State.SelectionStart : -1;
        var selEnd = State.HasSelection ? State.SelectionEnd : -1;

        for (var row = 0; row < viewportHeight; row++)
        {
            var visualIdx = VerticalScrollOffset + row;
            var screenY = Bounds.Y + row;

            if (visualIdx >= visualLines.Count)
            {
                // Empty line — fill with background
                var emptyLine = new string(' ', viewportWidth);
                var output = $"{globalColors}{fillBgAnsi}{emptyLine}{resetToGlobal}";
                if (context.CurrentLayoutProvider != null)
                    context.WriteClipped(Bounds.X, screenY, output);
                else
                    context.Write(output);
                continue;
            }

            var vLine = visualLines[visualIdx];
            var lineText = vLine.Text;
            var lineDocStart = vLine.DocStartOffset;
            var lineDocEnd = lineDocStart + lineText.Length;

            // Compute cursor column within this visual line (if cursor is on this line)
            var cursorOnThisLine = cursor >= lineDocStart && cursor <= lineDocEnd;
            var cursorCol = cursorOnThisLine ? cursor - lineDocStart : -1;

            // Compute selection range clipped to this visual line
            var lineSelStart = -1;
            var lineSelEnd = -1;
            if (selStart >= 0 && selEnd > selStart && selEnd > lineDocStart && selStart < lineDocEnd)
            {
                lineSelStart = Math.Max(0, selStart - lineDocStart);
                lineSelEnd = Math.Min(lineText.Length, selEnd - lineDocStart);
            }

            // Per-line horizontal viewport scrolling (non-word-wrap only).
            // When word wrap is off, each line can extend beyond the viewport width.
            // The cursor line scrolls to keep the cursor visible; other lines snap to offset 0.
            if (!IsWordWrap && lineText.Length > viewportWidth)
            {
                var hScroll = 0;
                if (cursorOnThisLine && cursorCol >= viewportWidth)
                {
                    // Scroll so cursor is visible at the right edge
                    hScroll = cursorCol - viewportWidth + 1;
                }

                var visEnd = Math.Min(hScroll + viewportWidth, lineText.Length);
                lineText = lineText[hScroll..visEnd];

                // Adjust cursor and selection positions for the viewport slice
                if (cursorCol >= 0)
                    cursorCol -= hScroll;
                if (lineSelStart >= 0)
                {
                    lineSelStart = Math.Clamp(lineSelStart - hScroll, 0, lineText.Length);
                    lineSelEnd = Math.Clamp(lineSelEnd - hScroll, 0, lineText.Length);
                    if (lineSelStart >= lineSelEnd)
                    {
                        lineSelStart = -1;
                        lineSelEnd = -1;
                    }
                }
            }

            var lineOutput = RenderMultilineLine(lineText, cursorCol, lineSelStart, lineSelEnd,
                viewportWidth, globalColors, resetToGlobal, fillBgAnsi,
                cursorFg, cursorBg, selFg, selBg, screenY);

            if (context.CurrentLayoutProvider != null)
                context.WriteClipped(Bounds.X, screenY, lineOutput);
            else
                context.Write(lineOutput);
        }
    }

    /// <summary>
    /// Renders a single line of a multiline text box with cursor and selection highlighting.
    /// </summary>
    private string RenderMultilineLine(string lineText, int cursorCol, int selStart, int selEnd,
        int viewportWidth, string globalColors, string resetToGlobal, string fillBgAnsi,
        Hex1bColor cursorFg, Hex1bColor cursorBg, Hex1bColor selFg, Hex1bColor selBg,
        int screenY)
    {
        var textDisplayWidth = DisplayWidth.GetStringWidth(lineText);
        var padding = Math.Max(0, viewportWidth - textDisplayWidth);

        if (IsFocused && cursorCol >= 0)
        {
            if (selStart >= 0 && selEnd > selStart)
            {
                // Selection on this line
                var beforeSel = lineText[..selStart];
                var selected = lineText[selStart..selEnd];
                var afterSel = lineText[selEnd..];
                var padStr = new string(' ', padding);
                return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
            }
            else
            {
                // Line caret: render text normally, position native cursor
                var before = lineText[..cursorCol];
                var after = cursorCol < lineText.Length ? lineText[cursorCol..] : "";

                ScreenCursorX = Bounds.X + DisplayWidth.GetStringWidth(before);
                ScreenCursorY = screenY;

                var padStr = new string(' ', padding);
                return $"{globalColors}{fillBgAnsi}{before}{after}{padStr}{resetToGlobal}";
            }
        }
        else if (selStart >= 0 && selEnd > selStart)
        {
            // Selection on this line but not focused (shouldn't normally happen, but handle gracefully)
            var beforeSel = lineText[..selStart];
            var selected = lineText[selStart..selEnd];
            var afterSel = lineText[selEnd..];
            var padStr = new string(' ', padding);
            return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
        }
        else
        {
            // No cursor, no selection on this line
            var padStr = new string(' ', padding);
            return $"{globalColors}{fillBgAnsi}{lineText}{padStr}{resetToGlobal}";
        }
    }

    #endregion

    public override void Render(Hex1bRenderContext context)
    {
        // Reset native cursor request — will be set if focused and no selection
        ScreenCursorX = -1;

        var theme = context.Theme;
        var cursorFg = theme.Get(TextBoxTheme.CursorForegroundColor);
        var cursorBg = theme.Get(TextBoxTheme.CursorBackgroundColor);
        var selFg = theme.Get(TextBoxTheme.SelectionForegroundColor);
        var selBg = theme.Get(TextBoxTheme.SelectionBackgroundColor);
        var fillBg = IsFocused
            ? theme.Get(TextBoxTheme.FocusedFillBackgroundColor)
            : theme.Get(TextBoxTheme.FillBackgroundColor);
        var predictionFg = theme.Get(TextBoxTheme.PredictionForegroundColor);
        var predictionBgRaw = theme.Get(TextBoxTheme.PredictionBackgroundColor);
        // Treat the Default sentinel as "follow the textbox fill background"
        // so the suggestion blends into the input surface unless the user
        // explicitly themed it to a contrasting color.
        var predictionBg = predictionBgRaw.IsDefault ? fillBg : predictionBgRaw;

        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();

        // Multiline text boxes use their own render path. Predictions are
        // single-line only by design.
        if (IsMultiline)
        {
            RenderMultiline(context, globalColors, resetToGlobal, fillBg, cursorFg, cursorBg, selFg, selBg);
            return;
        }

        // Inline (no-bracket) viewport: full bounds width is available for text.
        var viewportWidth = Bounds.Width;
        var textLen = State.Text.Length;

        // When bounds haven't been set (direct Render without layout) or the
        // text fits, show the full text without viewport slicing.
        var needsViewport = viewportWidth > 0 && textLen > viewportWidth;

        string visibleText;
        int visCursor, visSelStart, visSelEnd;

        if (needsViewport)
        {
            EnsureCursorInViewport(viewportWidth);
            (visibleText, visCursor, visSelStart, visSelEnd) = GetViewportSlice(viewportWidth);
        }
        else
        {
            ScrollOffset = 0;
            visibleText = State.Text;
            visCursor = State.CursorPosition;
            visSelStart = State.HasSelection ? State.SelectionStart : 0;
            visSelEnd = State.HasSelection ? State.SelectionEnd : 0;
        }

        var output = RenderInline(visibleText, visCursor, globalColors, resetToGlobal,
            fillBg, cursorFg, cursorBg, selFg, selBg, predictionFg, predictionBg, context,
            visSelStart, visSelEnd);

        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    /// <summary>
    /// Renders the single-line text box: background-filled, no bookend
    /// characters, with optional inline prediction overlay drawn after the
    /// cursor in the prediction colors.
    /// </summary>
    private string RenderInline(
        string text,
        int cursor,
        string globalColors,
        string resetToGlobal,
        Hex1bColor fillBg,
        Hex1bColor cursorFg,
        Hex1bColor cursorBg,
        Hex1bColor selFg,
        Hex1bColor selBg,
        Hex1bColor predictionFg,
        Hex1bColor predictionBg,
        Hex1bRenderContext context,
        int visSelStart = 0,
        int visSelEnd = 0)
    {
        var measuredWidth = Bounds.Width;
        var textDisplayWidth = DisplayWidth.GetStringWidth(text);
        var fillBgAnsi = fillBg.ToBackgroundAnsi();

        // Resolve the (possibly clipped) inline prediction overlay. Only valid
        // when focused, no selection, cursor at end of *real* buffer, and the
        // visible slice already contains the whole buffer.
        string? predictionOverlay = null;
        var canShowPrediction =
            IsFocused
            && !string.IsNullOrEmpty(CurrentPrediction)
            && !State.HasSelection
            && State.CursorPosition == State.Text.Length
            && cursor == text.Length;

        if (canShowPrediction)
        {
            var remaining = Math.Max(0, measuredWidth - textDisplayWidth);
            if (remaining > 0)
            {
                predictionOverlay = ClipToDisplayWidth(CurrentPrediction!, remaining);
            }
        }

        var predictionDisplayWidth = predictionOverlay is null ? 0 : DisplayWidth.GetStringWidth(predictionOverlay);
        var padding = Math.Max(0, measuredWidth - textDisplayWidth - predictionDisplayWidth);
        var predictionFgAnsi = predictionFg.ToForegroundAnsi();
        var predictionBgAnsi = predictionBg.ToBackgroundAnsi();
        var padStr = padding > 0 ? new string(' ', padding) : "";

        if (IsFocused)
        {
            if (State.HasSelection && visSelStart != visSelEnd)
            {
                var beforeSel = text[..visSelStart];
                var selected = text[visSelStart..visSelEnd];
                var afterSel = text[visSelEnd..];

                // Selection rendering masks the cursor cell — predictions are
                // suppressed when there's a selection so no overlay here.
                return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
            }

            // Line caret: render the text, then the prediction (if any) on the
            // resolved prediction background, and let the native terminal cursor
            // sit between them. The caller normalizes a Default prediction
            // background to fillBg so the suggestion blends with the field.
            var before = text[..cursor];
            var after = cursor < text.Length ? text[cursor..] : "";

            ScreenCursorX = Bounds.X + DisplayWidth.GetStringWidth(before);
            ScreenCursorY = Bounds.Y;

            if (predictionOverlay is not null)
            {
                return $"{globalColors}{fillBgAnsi}{before}{after}{predictionFgAnsi}{predictionBgAnsi}{predictionOverlay}{resetToGlobal}{fillBgAnsi}{padStr}{resetToGlobal}";
            }

            return $"{globalColors}{fillBgAnsi}{before}{after}{padStr}{resetToGlobal}";
        }

        // Unfocused — no cursor, no prediction.
        return $"{globalColors}{fillBgAnsi}{text}{padStr}{resetToGlobal}";
    }

    /// <summary>
    /// Returns a prefix of <paramref name="value"/> whose total display width
    /// is at most <paramref name="maxDisplayWidth"/>. Wide graphemes are kept
    /// whole — they're either fully included or omitted, never split.
    /// </summary>
    private static string ClipToDisplayWidth(string value, int maxDisplayWidth)
    {
        if (maxDisplayWidth <= 0 || string.IsNullOrEmpty(value)) return string.Empty;

        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(value);
        var consumed = 0;
        var lastFitEnd = 0;
        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var width = DisplayWidth.GetGraphemeWidth(grapheme);
            if (consumed + width > maxDisplayWidth)
                break;
            consumed += width;
            lastFitEnd = enumerator.ElementIndex + grapheme.Length;
        }
        return lastFitEnd >= value.Length ? value : value[..lastFitEnd];
    }

}
