using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the inline predictive-completion behavior of <see cref="TextBoxNode"/>.
/// </summary>
public class TextBoxPredictionTests
{
    private static TextBoxNode CreateNode(
        Func<string, CancellationToken, Task<string?>>? predictor = null,
        TimeSpan debounce = default,
        bool focused = true)
    {
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = focused,
            Predictor = predictor,
            PredictionDebounce = debounce,
        };
        return node;
    }

    /// <summary>
    /// Drives the node through one keystroke, then waits up to
    /// <paramref name="timeout"/> for the predictor's async result to be
    /// applied. The node only mutates <c>CurrentPrediction</c> from the
    /// predictor task, so direct equality checks would race without this.
    /// </summary>
    private static async Task<string?> WaitForPredictionAsync(TextBoxNode node, TimeSpan timeout, Func<string?, bool>? predicate = null)
    {
        predicate ??= p => p is not null;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate(node.CurrentPrediction)) return node.CurrentPrediction;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        return node.CurrentPrediction;
    }

    private static Hex1bKeyEvent CharKey(char ch) => new((Hex1bKey)char.ToUpperInvariant(ch), ch, Hex1bModifiers.None);

    [Fact]
    public async Task Typing_AtEndOfBuffer_TriggersPredictor()
    {
        var calls = 0;
        var seen = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var node = CreateNode((text, _) =>
        {
            Interlocked.Increment(ref calls);
            seen.Enqueue(text);
            return Task.FromResult<string?>("world");
        });

        await InputRouter.RouteInputToNodeAsync(node, CharKey('h'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('e'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('l'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('l'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('o'), null, null, TestContext.Current.CancellationToken);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline && !seen.Contains("hello"))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var prediction = await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));

        Assert.Equal("world", prediction);
        Assert.True(seen.Contains("hello"), $"Predictor never saw 'hello'. text='{node.Text}' seen=[{string.Join(",", seen)}] calls={calls}");
        Assert.True(calls >= 1);
    }

    [Fact]
    public async Task Typing_WithCursorInMiddle_DoesNotTriggerPredictor()
    {
        var calls = 0;
        var node = new TextBoxNode
        {
            Text = "abcd",
            IsFocused = true,
            Predictor = (_, _) => { Interlocked.Increment(ref calls); return Task.FromResult<string?>("zzz"); },
        };
        node.State.CursorPosition = 2;

        // Typing in the middle of the buffer should not request a prediction.
        await InputRouter.RouteInputToNodeAsync(node, CharKey('X'), null, null, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, calls);
        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task LeftArrow_ClearsActivePrediction()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("xyz"));
        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));

        Assert.Equal("xyz", node.CurrentPrediction);

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task Backspace_ClearsActivePrediction()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("xyz"));
        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));
        Assert.Equal("xyz", node.CurrentPrediction);

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task FocusLoss_ClearsActivePrediction()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("xyz"));
        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));
        Assert.Equal("xyz", node.CurrentPrediction);

        node.IsFocused = false;

        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task MouseClick_ClearsActivePrediction()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("xyz"));
        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));
        Assert.Equal("xyz", node.CurrentPrediction);

        node.Arrange(new Rect(0, 0, 10, 1));
        node.HandleMouseClick(0, 0, new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None, ClickCount: 1));

        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task Escape_WithActivePrediction_IsConsumed_AndClears()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("xyz"));
        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));
        Assert.Equal("xyz", node.CurrentPrediction);

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Null(node.CurrentPrediction);
    }

    [Fact]
    public async Task Escape_WithoutActivePrediction_IsNotConsumed()
    {
        var node = CreateNode();

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task RightArrow_WithActivePrediction_AcceptsAndFiresChange()
    {
        var node = CreateNode((_, _) => Task.FromResult<string?>("world"));
        var changes = 0;
        string? lastNew = null;
        node.TextChangedAction = (_, _, newText) =>
        {
            Interlocked.Increment(ref changes);
            lastNew = newText;
            return Task.CompletedTask;
        };

        await InputRouter.RouteInputToNodeAsync(node, CharKey('h'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('e'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('l'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('l'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('o'), null, null, TestContext.Current.CancellationToken);

        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));
        Assert.Equal("world", node.CurrentPrediction);

        var typingChanges = changes;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("helloworld", node.Text);
        Assert.Equal(node.Text.Length, node.State.CursorPosition);
        Assert.Null(node.CurrentPrediction);
        Assert.Equal(typingChanges + 1, changes);
        Assert.Equal("helloworld", lastNew);
    }

    [Fact]
    public async Task RightArrow_WithoutPrediction_MovesCursor()
    {
        var node = new TextBoxNode { Text = "abc", IsFocused = true };
        node.State.CursorPosition = 1;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, node.State.CursorPosition);
    }

    [Fact]
    public async Task StaleAsyncResult_IsDiscarded()
    {
        // The predictor deliberately *ignores* the cancellation token. The
        // first call captures snapshot "a" and sleeps for 60 ms before
        // returning "a!". By the time it returns, the user has typed another
        // character and the buffer is "ab"; the snapshot guard inside the
        // node must drop the stale "a!" result so the predictor that
        // captured "ab" wins.
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = true,
            Predictor = async (text, _) =>
            {
                await Task.Delay(60);
                return text + "!";
            },
        };

        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('b'), null, null, TestContext.Current.CancellationToken);

        var prediction = await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));

        // Whichever order the two predictor tasks complete in, the value
        // tied to the stale snapshot ("a!") must never become the active
        // prediction. Only the value tied to the live snapshot ("ab!")
        // is allowed.
        Assert.Equal("ab!", prediction);
    }

    [Fact]
    public async Task Debounce_CoalescesRapidKeystrokesIntoSingleCall()
    {
        var calls = 0;
        string? lastSeen = null;
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = true,
            PredictionDebounce = TimeSpan.FromMilliseconds(80),
            Predictor = (text, _) =>
            {
                Interlocked.Increment(ref calls);
                lastSeen = text;
                return Task.FromResult<string?>(text + "!");
            },
        };

        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('b'), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, CharKey('c'), null, null, TestContext.Current.CancellationToken);

        await WaitForPredictionAsync(node, TimeSpan.FromSeconds(2));

        Assert.Equal(1, calls);
        Assert.Equal("abc", lastSeen);
        Assert.Equal("abc!", node.CurrentPrediction);
    }

    [Fact]
    public async Task Multiline_NeverPopulatesPrediction()
    {
        var calls = 0;
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = true,
            IsMultiline = true,
            Predictor = (_, _) => { Interlocked.Increment(ref calls); return Task.FromResult<string?>("xxx"); },
        };
        node.State.IsMultiline = true;

        await InputRouter.RouteInputToNodeAsync(node, CharKey('a'), null, null, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Null(node.CurrentPrediction);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PredictWidget_Reconciles_PredictorOntoNode()
    {
        var widget = new TextBoxWidget("hello").Predict((_, _) => Task.FromResult<string?>("ignored"));
        var node = new TextBoxNode();
        var ctx = ReconcileContext.CreateRoot(cancellationToken: TestContext.Current.CancellationToken);
        ctx.IsNew = true;

        await widget.ReconcileAsync(node, ctx);

        Assert.NotNull(node.Predictor);
        Assert.Equal(TimeSpan.Zero, node.PredictionDebounce);
    }

    [Fact]
    public async Task PredictWidget_WithDebounce_FlowsDebounceOntoNode()
    {
        var widget = new TextBoxWidget("hello").Predict((_, _) => Task.FromResult<string?>("ignored"), TimeSpan.FromMilliseconds(123));
        var node = new TextBoxNode();
        var ctx = ReconcileContext.CreateRoot(cancellationToken: TestContext.Current.CancellationToken);
        ctx.IsNew = true;

        await widget.ReconcileAsync(node, ctx);

        Assert.Equal(TimeSpan.FromMilliseconds(123), node.PredictionDebounce);
    }
}
