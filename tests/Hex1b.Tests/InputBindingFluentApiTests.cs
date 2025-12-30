using Hex1b.Input;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for input bindings using the fluent API, ensuring all keys fire correctly
/// after reconciliation has occurred.
/// </summary>
public class InputBindingFluentApiTests
{
    /// <summary>
    /// Returns all Hex1bKey enum values except None, for use in Theory tests.
    /// </summary>
    public static TheoryData<Hex1bKey> AllHex1bKeys
    {
        get
        {
            var data = new TheoryData<Hex1bKey>();
            foreach (var key in Enum.GetValues<Hex1bKey>())
            {
                if (key != Hex1bKey.None)
                {
                    data.Add(key);
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllHex1bKeys))]
    public async Task InputBinding_FluentApi_FiresForAllKeys(Hex1bKey key)
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        Hex1bKey? firedKey = null;
        var reconcileOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                // Create a test widget wrapped in a VStack (for focus support)
                // with an input binding for the specific key being tested
                var testWidget = ctx.Test()
                    .OnReconcile(_ => reconcileOccurred.TrySetResult())
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(key).Action(_ =>
                        {
                            bindingFired = true;
                            firedKey = key;
                            return Task.CompletedTask;
                        }, $"Test binding for {key}");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for reconciliation to occur (ensures the app has processed at least one cycle)
        await reconcileOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Wait for render to occur
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act - Send the key
        await new Hex1bTerminalInputSequenceBuilder().Key(key).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Wait a bit for the input to be processed
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bindingFired, $"Expected binding to fire for key {key}");
        Assert.Equal(key, firedKey);

        // Clean up
        cts.Cancel();
        await runTask;
    }

    [Theory]
    [MemberData(nameof(AllHex1bKeys))]
    public async Task InputBinding_FluentApi_WithCtrlModifier_FiresForAllKeys(Hex1bKey key)
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var reconcileOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnReconcile(_ => reconcileOccurred.TrySetResult())
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Ctrl().Key(key).Action(_ =>
                        {
                            bindingFired = true;
                            return Task.CompletedTask;
                        }, $"Test Ctrl+{key}");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await reconcileOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act - Send the key with Ctrl modifier
        await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(key).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bindingFired, $"Expected binding to fire for Ctrl+{key}");

        cts.Cancel();
        await runTask;
    }

    [Theory]
    [MemberData(nameof(AllHex1bKeys))]
    public async Task InputBinding_FluentApi_WithShiftModifier_FiresForAllKeys(Hex1bKey key)
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var reconcileOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnReconcile(_ => reconcileOccurred.TrySetResult())
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Shift().Key(key).Action(_ =>
                        {
                            bindingFired = true;
                            return Task.CompletedTask;
                        }, $"Test Shift+{key}");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await reconcileOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act - Send the key with Shift modifier
        await new Hex1bTerminalInputSequenceBuilder().Shift().Key(key).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bindingFired, $"Expected binding to fire for Shift+{key}");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task InputBinding_FluentApi_MultipleBindings_EachFiresCorrectly()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var aFired = false;
        var bFired = false;
        var cFired = false;
        var reconcileOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnReconcile(_ => reconcileOccurred.TrySetResult())
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.A).Action(_ => { aFired = true; return Task.CompletedTask; }, "A");
                        bindings.Key(Hex1bKey.B).Action(_ => { bFired = true; return Task.CompletedTask; }, "B");
                        bindings.Key(Hex1bKey.C).Action(_ => { cFired = true; return Task.CompletedTask; }, "C");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await reconcileOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act - Send each key
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A).Wait(50)
            .Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.C).Wait(50)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(aFired, "Expected A binding to fire");
        Assert.True(bFired, "Expected B binding to fire");
        Assert.True(cFired, "Expected C binding to fire");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public void InputBinding_WithInputBindings_SetsConfiguratorOnWidget()
    {
        // Verify that WithInputBindings actually sets the configurator on the widget
        var configured = false;
        
        var widget = new VStackWidget([])
            .WithInputBindings(bindings =>
            {
                configured = true;
                bindings.Key(Hex1bKey.X).Action(() => { }, "Test");
            });
        
        // Check that BindingsConfigurator is set (internal property)
        var configuratorField = typeof(Hex1bWidget).GetProperty("BindingsConfigurator", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(configuratorField);
        
        var configurator = configuratorField!.GetValue(widget) as Action<InputBindingsBuilder>;
        Assert.NotNull(configurator);
        
        // Invoke it and verify it runs
        var builder = new InputBindingsBuilder();
        configurator!(builder);
        
        Assert.True(configured, "The configurator callback should have been invoked");
        Assert.True(builder.Bindings.Count >= 1, "At least one binding should have been added");
    }
    
    [Fact]
    public async Task InputBinding_DiagnosticTest_VerifyBindingsAreSet()
    {
        // This test verifies that user bindings configured via WithInputBindings work
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var xBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.X).Action(_ => 
                        { 
                            xBindingFired = true; 
                            return Task.CompletedTask; 
                        }, "Test X");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for render (reconciliation has happened)
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Send X key
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait for input processing
        await Task.Delay(200, TestContext.Current.CancellationToken);
        
        // Check if X binding fired
        Assert.True(xBindingFired, "User binding for X key should have fired");

        cts.Cancel();
        await runTask;
    }
    
    [Fact]
    public async Task InputBinding_DiagnosticTest_UserBindingWithCtrlCDisabled()
    {
        // Test with CTRL-C disabled to isolate user bindings
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var xBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.X).Action(_ => 
                        { 
                            xBindingFired = true; 
                            return Task.CompletedTask; 
                        }, "Test X");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for render
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Send X key
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait for input processing
        await Task.Delay(200, TestContext.Current.CancellationToken);
        
        // Check if X binding fired
        Assert.True(xBindingFired, "User binding for X key should have fired (CTRL-C disabled)");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task InputBinding_FluentApi_ChordBinding_FiresOnSecondKey()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var chordFired = false;
        var reconcileOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnReconcile(_ => reconcileOccurred.TrySetResult())
                    .OnRender(_ => renderOccurred.TrySetResult());

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        // Create a chord: Ctrl+K, then Ctrl+C
                        bindings.Ctrl().Key(Hex1bKey.K)
                            .Then().Ctrl().Key(Hex1bKey.C)
                            .Action(_ => { chordFired = true; return Task.CompletedTask; }, "Kill line");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await reconcileOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act - Send the chord sequence
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.K).Wait(50)
            .Ctrl().Key(Hex1bKey.C).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(chordFired, "Expected chord binding to fire");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task InputBinding_ReconciliationPreservesBindings()
    {
        // Arrange - verify that bindings are preserved across reconciliation
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFireCount = 0;
        var reconcileCount = 0;

        using var app = new Hex1bApp(
            ctx =>
            {
                var testWidget = ctx.Test()
                    .OnReconcile(_ => Interlocked.Increment(ref reconcileCount));

                var vstack = new VStackWidget([testWidget])
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.X).Action(_ =>
                        {
                            Interlocked.Increment(ref bindingFireCount);
                            return Task.CompletedTask;
                        }, "X key");
                    });

                return Task.FromResult<Hex1bWidget>(vstack);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for initial reconciliation
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(reconcileCount >= 1, "Expected at least one reconciliation");

        // Fire binding before re-render
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Capture("before-rerender").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, bindingFireCount);

        // Force a re-render by invalidating
        app.Invalidate();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(reconcileCount >= 2, "Expected second reconciliation after invalidate");

        // Fire binding again after re-render
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Capture("after-rerender").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(2, bindingFireCount);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Gets an appropriate character for a given key, used for simulating key events.
    /// </summary>
    private static char GetKeyChar(Hex1bKey key) => key switch
    {
        // Letters (lowercase)
        Hex1bKey.A => 'a',
        Hex1bKey.B => 'b',
        Hex1bKey.C => 'c',
        Hex1bKey.D => 'd',
        Hex1bKey.E => 'e',
        Hex1bKey.F => 'f',
        Hex1bKey.G => 'g',
        Hex1bKey.H => 'h',
        Hex1bKey.I => 'i',
        Hex1bKey.J => 'j',
        Hex1bKey.K => 'k',
        Hex1bKey.L => 'l',
        Hex1bKey.M => 'm',
        Hex1bKey.N => 'n',
        Hex1bKey.O => 'o',
        Hex1bKey.P => 'p',
        Hex1bKey.Q => 'q',
        Hex1bKey.R => 'r',
        Hex1bKey.S => 's',
        Hex1bKey.T => 't',
        Hex1bKey.U => 'u',
        Hex1bKey.V => 'v',
        Hex1bKey.W => 'w',
        Hex1bKey.X => 'x',
        Hex1bKey.Y => 'y',
        Hex1bKey.Z => 'z',

        // Numbers
        Hex1bKey.D0 => '0',
        Hex1bKey.D1 => '1',
        Hex1bKey.D2 => '2',
        Hex1bKey.D3 => '3',
        Hex1bKey.D4 => '4',
        Hex1bKey.D5 => '5',
        Hex1bKey.D6 => '6',
        Hex1bKey.D7 => '7',
        Hex1bKey.D8 => '8',
        Hex1bKey.D9 => '9',

        // Numpad numbers
        Hex1bKey.NumPad0 => '0',
        Hex1bKey.NumPad1 => '1',
        Hex1bKey.NumPad2 => '2',
        Hex1bKey.NumPad3 => '3',
        Hex1bKey.NumPad4 => '4',
        Hex1bKey.NumPad5 => '5',
        Hex1bKey.NumPad6 => '6',
        Hex1bKey.NumPad7 => '7',
        Hex1bKey.NumPad8 => '8',
        Hex1bKey.NumPad9 => '9',

        // Numpad operators
        Hex1bKey.Multiply => '*',
        Hex1bKey.Add => '+',
        Hex1bKey.Subtract => '-',
        Hex1bKey.Decimal => '.',
        Hex1bKey.Divide => '/',

        // Whitespace
        Hex1bKey.Tab => '\t',
        Hex1bKey.Enter => '\r',
        Hex1bKey.Spacebar => ' ',

        // Punctuation
        Hex1bKey.OemComma => ',',
        Hex1bKey.OemPeriod => '.',
        Hex1bKey.OemMinus => '-',
        Hex1bKey.OemPlus => '=',
        Hex1bKey.OemQuestion => '/',
        Hex1bKey.Oem1 => ';',
        Hex1bKey.Oem4 => '[',
        Hex1bKey.Oem5 => '\\',
        Hex1bKey.Oem6 => ']',
        Hex1bKey.Oem7 => '\'',
        Hex1bKey.OemTilde => '`',

        // Non-printable keys
        Hex1bKey.Escape => '\x1b',
        Hex1bKey.Backspace => '\b',
        Hex1bKey.Delete => '\x7f',

        // Function and navigation keys don't have printable chars
        _ => '\0',
    };
}
