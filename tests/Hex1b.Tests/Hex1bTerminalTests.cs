using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminal virtual terminal emulator.
/// </summary>
public class Hex1bTerminalTests
{
    private sealed class QueuedInputPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public void EnqueueInput(string text)
            => _input.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            while (await _input.Reader.WaitToReadAsync(ct))
            {
                if (_input.Reader.TryRead(out var data))
                    return data;
            }

            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);

        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingInputPresentationAdapter(Exception exception) : IHex1bTerminalPresentationAdapter
    {
        private readonly Exception _exception = exception;
        private bool _hasThrown;

        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            if (!_hasThrown)
            {
                _hasThrown = true;
                return ValueTask.FromException<ReadOnlyMemory<byte>>(_exception);
            }

            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingWorkloadAdapter(Exception exception) : IHex1bTerminalWorkloadAdapter
    {
        private readonly Exception _exception = exception;

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.FromException(_exception);

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingResizeWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public int? ResizeWidth { get; private set; }
        public int? ResizeHeight { get; private set; }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            ResizeWidth = width;
            ResizeHeight = height;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Constructor_InitializesWithCorrectDimensions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public async Task Constructor_InitializesWithEmptyScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Equal("", line);
    }

    [Fact]
    public async Task Constructor_WithResizedTerminalWidgetHandle_UsesHandleDimensionsForInitialWorkloadResize()
    {
        await using var presentation = new TerminalWidgetHandle(80, 24);
        await using var workload = new RecordingResizeWorkloadAdapter();

        presentation.Resize(132, 41);

        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        Assert.Equal(132, terminal.Width);
        Assert.Equal(41, terminal.Height);
        Assert.Equal(132, workload.ResizeWidth);
        Assert.Equal(41, workload.ResizeHeight);
    }

    [Fact]
    public async Task Write_PlacesTextAtCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("Hello");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"),
                TimeSpan.FromSeconds(1), "Hello text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.Equal("Hello", snapshot.GetLineTrimmed(0));
        Assert.Equal(5, snapshot.CursorX);
        Assert.Equal(0, snapshot.CursorY);
    }

    [Theory]
    [InlineData("\x1b_Gi=123", ";OK\x1b\\")]
    [InlineData("\x1b_Gi=123;OK\x1b", "\\")]
    public async Task PresentationInput_SplitKgpResponse_DoesNotEmitEscapeKeyEvent(string firstChunk, string secondChunk)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput(firstChunk);
        presentation.EnqueueInput(secondChunk);
        presentation.EnqueueInput("a");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var keyEvent = Assert.IsType<Hex1bKeyEvent>(evt);
        Assert.Equal(Hex1bKey.A, keyEvent.Key);
        Assert.Equal("a", keyEvent.Text);
        Assert.Equal(Hex1bModifiers.None, keyEvent.Modifiers);
        Assert.False(workload.InputEvents.TryRead(out _));
    }

    [Fact]
    public async Task PresentationInput_SplitSs3Sequence_DoesNotEmitAltKeyEvent()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput("\x1bO");
        presentation.EnqueueInput("A");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var keyEvent = Assert.IsType<Hex1bKeyEvent>(evt);
        Assert.Equal(Hex1bKey.UpArrow, keyEvent.Key);
        Assert.Equal(Hex1bModifiers.None, keyEvent.Modifiers);
        Assert.False(workload.InputEvents.TryRead(out _));
    }

    [Fact]
    public async Task RunAsync_WhenPresentationInputPumpThrows_SurfacesTheFailure()
    {
        await using var presentation = new ThrowingInputPresentationAdapter(
            new InvalidOperationException("synthetic input failure"));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => terminal.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("presentation input", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.InnerException);
        Assert.Contains("synthetic input failure", ex.InnerException!.Message);
    }

    [Fact]
    public async Task RunAsync_WhenWorkloadWriteInputThrows_SurfacesTheFailure()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        await using var workload = new ThrowingWorkloadAdapter(
            new IOException("synthetic shim send failure"));
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput("x");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => terminal.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("presentation input", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.InnerException);
        Assert.Contains("synthetic shim send failure", ex.InnerException!.Message);
    }

    [Theory]
    [InlineData("\x1b_Gi=123", ";OK\x1b\\")]
    [InlineData("\x1b_Gi=123;OK\x1b", "\\")]
    public async Task AppInput_SplitKgpResponse_DoesNotTriggerEscapeBinding(string firstChunk, string secondChunk)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var normalKeyHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escapeTriggered = false;
        var status = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget(status)
                ]).WithInputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        escapeTriggered = true;
                        status = "Escape triggered";
                        return Task.CompletedTask;
                    }, "Escape binding");

                    bindings.Key(Hex1bKey.A).Action(_ =>
                    {
                        status = "Normal key handled";
                        normalKeyHandled.TrySetResult();
                        return Task.CompletedTask;
                    }, "A binding");
                })
            ),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2), "initial render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        presentation.EnqueueInput(firstChunk);
        presentation.EnqueueInput(secondChunk);
        presentation.EnqueueInput("a");

        await normalKeyHandled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Normal key handled"), TimeSpan.FromSeconds(2), "normal key handled")
            .Capture("after-split-kgp-response")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(escapeTriggered);
        Assert.True(snapshot.ContainsText("Normal key handled"));

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task PresentationInput_BareEscape_FlushedAfterTimeout()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        // Send just \x1b with no continuation — after the timeout the
        // terminal should flush it as a standalone Escape key event.
        presentation.EnqueueInput("\x1b");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = Assert.IsType<Hex1bKeyEvent>(evt);
        Assert.Equal(Hex1bKey.Escape, keyEvent.Key);
        Assert.Equal(Hex1bModifiers.None, keyEvent.Modifiers);
    }

    [Fact]
    public async Task PresentationInput_DoubleEscape_DoesNotKillEventPump()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        // Send two bare escapes back-to-back, then a normal key.
        // The pump must survive both timeouts and still dispatch the 'a'.
        presentation.EnqueueInput("\x1b");
        await Task.Delay(TimeSpan.FromMilliseconds(70));
        presentation.EnqueueInput("\x1b");
        await Task.Delay(TimeSpan.FromMilliseconds(70));
        presentation.EnqueueInput("a");

        var events = new List<Hex1bKeyEvent>();
        for (int i = 0; i < 3; i++)
        {
            var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            events.Add(Assert.IsType<Hex1bKeyEvent>(evt));
        }

        Assert.Equal(Hex1bKey.Escape, events[0].Key);
        Assert.Equal(Hex1bKey.Escape, events[1].Key);
        Assert.Equal(Hex1bKey.A, events[2].Key);
    }

    [Fact]
    public async Task PresentationInput_CustomEscapeTimeout_UsesConfiguredValue()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24,
            EscapeSequenceTimeout = TimeSpan.FromMilliseconds(10)
        });

        presentation.EnqueueInput("\x1b");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = Assert.IsType<Hex1bKeyEvent>(evt);
        Assert.Equal(Hex1bKey.Escape, keyEvent.Key);
    }

    [Fact]
    public async Task PresentationInput_ZeroEscapeTimeout_DisablesFlush()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24,
            EscapeSequenceTimeout = TimeSpan.Zero
        });

        // Send bare \x1b — with timeout disabled, it should stay buffered.
        presentation.EnqueueInput("\x1b");

        // Wait well beyond the default 50ms timeout
        await Task.Delay(150);

        // No event should have been dispatched
        Assert.False(workload.InputEvents.TryRead(out _),
            "With EscapeSequenceTimeout=Zero, bare ESC should stay buffered");

        // Sending a continuation byte should produce the combined sequence (Alt+A)
        presentation.EnqueueInput("a");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = Assert.IsType<Hex1bKeyEvent>(evt);
        Assert.Equal(Hex1bKey.A, keyEvent.Key);
        Assert.Equal(Hex1bModifiers.Alt, keyEvent.Modifiers);
    }

    [Fact]
    public async Task AppInput_BareEscape_TriggersEscapeBinding()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var escapeTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var status = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget(status)
                ]).WithInputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        status = "Escape handled";
                        escapeTriggered.TrySetResult();
                        return Task.CompletedTask;
                    }, "Escape binding");
                })
            ),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2), "initial render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Send bare \x1b — should be flushed as Escape after timeout
        presentation.EnqueueInput("\x1b");

        await escapeTriggered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(escapeTriggered.Task.IsCompletedSuccessfully, "Escape binding should have fired from bare \\x1b");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Write_HandlesNewlines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // Use \r\n (CRLF) - real terminals expect ONLCR translation to happen in PTY layer
        workload.Write("Line1\r\nLine2\r\nLine3");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line1") && s.ContainsText("Line2") && s.ContainsText("Line3"),
                TimeSpan.FromSeconds(1), "all three lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.Equal("Line1", snapshot.GetLineTrimmed(0));
        Assert.Equal("Line2", snapshot.GetLineTrimmed(1));
        Assert.Equal("Line3", snapshot.GetLineTrimmed(2));
    }

    [Fact]
    public async Task Write_WrapsAtEndOfLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();
        
        workload.Write("HelloWorld");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello") && s.ContainsText("World"),
                TimeSpan.FromSeconds(1), "wrapped text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.Equal("Hello", snapshot.GetLineTrimmed(0));
        Assert.Equal("World", snapshot.GetLineTrimmed(1));
    }

    [Fact]
    public async Task Clear_ResetsScreenAndCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some text");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Some text"),
                TimeSpan.FromSeconds(1), "initial text")
            .Build()
            .ApplyAsync(terminal);
        
        workload.Clear();
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal(0, terminal.CreateSnapshot().CursorX);
        Assert.Equal(0, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public async Task SetCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.SetCursorPosition(5, 2);
        workload.Write("X");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("X"),
                TimeSpan.FromSeconds(1), "X at cursor position")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var line = snapshot.GetLine(2);
        Assert.Equal('X', line[5]);
    }

    [Fact]
    public async Task SetCursorPosition_ClampsToBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        workload.SetCursorPosition(100, 100);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal(9, terminal.CreateSnapshot().CursorX);
        Assert.Equal(4, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public async Task EnterAlternateScreen_SetsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
        
        terminal.EnterAlternateScreen();
        
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task ExitAlternateScreen_ClearsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.EnterAlternateScreen();
        
        terminal.ExitAlternateScreen();
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task ContainsText_FindsText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        workload.Write("Hello World");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"),
                TimeSpan.FromSeconds(1), "Hello World text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(snapshot.ContainsText("World"));
        Assert.False(snapshot.ContainsText("Foo"));
    }

    [Fact]
    public async Task FindText_ReturnsPositions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n - terminal emulator expects explicit CR before LF
        workload.Write("Hello World\r\nHello Again");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World") && s.ContainsText("Hello Again"),
                TimeSpan.FromSeconds(1), "both Hello lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var results = snapshot.FindText("Hello");
        
        Assert.Equal(2, results.Count);
        Assert.Equal((0, 0), results[0]); // (Line, Column) = row 0, col 0
        Assert.Equal((1, 0), results[1]); // (Line, Column) = row 1, col 0
    }

    [Fact]
    public async Task GetNonEmptyLines_FiltersEmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n for proper line endings
        workload.Write("Line 1\r\n\r\nLine 3");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1") && s.ContainsText("Line 3"),
                TimeSpan.FromSeconds(1), "Line 1 and Line 3")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var lines = snapshot.GetNonEmptyLines().ToList();
        
        Assert.Equal(2, lines.Count);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 3", lines[1]);
    }

    [Fact]
    public async Task Resize_PreservesContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"),
                TimeSpan.FromSeconds(1), "Hello text")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        terminal.Resize(40, 10);
        
        Assert.Equal(40, terminal.Width);
        Assert.Equal(10, terminal.Height);
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public async Task AnsiSequences_AreProcessedButNotDisplayed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        
        workload.Write("\x1b[31mRed Text\x1b[0m");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Red Text"),
                TimeSpan.FromSeconds(1), "Red Text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.Equal("Red Text", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task AnsiCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // ANSI positions are 1-based, so row 2, col 5
        workload.Write("\x1b[2;5HX");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("X"),
                TimeSpan.FromSeconds(1), "X at ANSI position")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var line = snapshot.GetLine(1); // 0-based
        Assert.Equal('X', line[4]); // 0-based
    }

    [Fact]
    public async Task AnsiClearScreen_ClearsBuffer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some content");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Some content"),
                TimeSpan.FromSeconds(1), "initial content")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        workload.Write("\x1b[2J");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public async Task GetScreenBuffer_ReturnsCopyWithColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("\x1b[38;2;255;0;0mR\x1b[0m");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("R"),
                TimeSpan.FromSeconds(1), "R with red color")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var buffer = terminal.GetScreenBuffer();
        
        Assert.Equal("R", buffer[0, 0].Character);
        Assert.NotNull(buffer[0, 0].Foreground);
        Assert.Equal(255, buffer[0, 0].Foreground!.Value.R);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.G);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.B);
    }

    [Fact]
    public async Task AlternateScreenAnsiSequence_IsRecognized()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("\x1b[?1049h");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
        
        workload.Write("\x1b[?1049l");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    #region Resize Behavior

    [Fact]
    public async Task Constructor_SetsWorkloadDimensions()
    {
        // Workload dimensions are 0x0 before terminal is created
        using var workload = new Hex1bAppWorkloadAdapter();
        Assert.Equal(0, workload.Width);
        Assert.Equal(0, workload.Height);
        
        // Terminal sets workload dimensions during construction
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        Assert.Equal(80, workload.Width);
        Assert.Equal(24, workload.Height);
    }

    [Fact]
    public async Task Constructor_DoesNotFireResizeEvent()
    {
        // This is critical: the initial dimension setup should NOT fire a resize event
        // because that would trigger an extra re-render before the app even starts
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Try to read from input channel - should be empty (no resize event)
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.False(hasEvent, "Constructor should not fire a resize event");
    }

    [Fact]
    public async Task ResizeAsync_AfterInitialization_FiresResizeEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Now call ResizeAsync again (simulating a terminal resize)
        await workload.ResizeAsync(100, 30, TestContext.Current.CancellationToken);
        
        // This should fire a resize event
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.True(hasEvent, "ResizeAsync after init should fire a resize event");
        
        var resizeEvent = Assert.IsType<Hex1bResizeEvent>(evt);
        Assert.Equal(100, resizeEvent.Width);
        Assert.Equal(30, resizeEvent.Height);
    }

    [Fact]
    public async Task ResizeAsync_SameDimensions_DoesNotFireEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Resize to same dimensions
        await workload.ResizeAsync(80, 24, TestContext.Current.CancellationToken);
        
        // Should NOT fire event (no change)
        var hasEvent = workload.InputEvents.TryRead(out _);
        Assert.False(hasEvent, "ResizeAsync with same dimensions should not fire event");
    }

    #endregion
}
