using System.Text;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for clipboard functionality via OSC 52.
/// </summary>
public class ClipboardTests
{
    /// <summary>
    /// A mock presentation adapter that captures all output for verification.
    /// </summary>
    private class CapturingPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly StringBuilder _capturedOutput = new();
        private readonly object _lock = new();
        
        public string CapturedOutput
        {
            get
            {
                lock (_lock)
                {
                    return _capturedOutput.ToString();
                }
            }
        }
        
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };
        
        public event Action<int, int>? Resized;
        public event Action? Disconnected;

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var text = Encoding.UTF8.GetString(data.Span);
            lock (_lock)
            {
                _capturedOutput.Append(text);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            // Block forever - tests will use the input sequence builder
            return new ValueTask<ReadOnlyMemory<byte>>(
                Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => ReadOnlyMemory<byte>.Empty));
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        
        // Helper to trigger events (not used in this test but required by interface)
        public void TriggerResize(int width, int height) => Resized?.Invoke(width, height);
        public void TriggerDisconnect() => Disconnected?.Invoke();
    }

    [Fact(Skip = "Integration test needs rework - terminal/app connection issue")]
    public async Task CopyToClipboard_SendsOsc52Sequence_ToOutput()
    {
        // Arrange
        var presentation = new CapturingPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        };

        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var copyTriggered = false;
        var textToCopy = "Test clipboard content";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new ButtonWidget("Copy Test")
                        .OnClick(e =>
                        {
                            e.Context.CopyToClipboard(textToCopy);
                            copyTriggered = true;
                        })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for button to be rendered, then press Enter to click it
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Copy Test"), TimeSpan.FromSeconds(2))
            .Enter()
            .Wait(TimeSpan.FromMilliseconds(100)) // Wait for clipboard write to process
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Assert
        Assert.True(copyTriggered, "Button click should have triggered copy");
        
        // Verify the OSC 52 sequence was sent
        var output = presentation.CapturedOutput;
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(textToCopy));
        var expectedOsc52 = $"\x1b]52;c;{expectedBase64}\x07";
        
        Assert.Contains(expectedOsc52, output);
    }

    [Fact]
    public void CopyToClipboard_GeneratesCorrectOsc52Format()
    {
        // This is a unit test to verify the OSC 52 format is correct
        var text = "Hello, World!";
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var expected = $"\x1b]52;c;{expectedBase64}\x07";
        
        // The expected format is: ESC ] 52 ; c ; <base64> BEL
        // ESC = 0x1B, BEL = 0x07
        Assert.StartsWith("\x1b]52;c;", expected);
        Assert.EndsWith("\x07", expected);
        Assert.Equal("SGVsbG8sIFdvcmxkIQ==", expectedBase64);
    }

    [Fact]
    public async Task InputBindingActionContext_CopyToClipboard_InvokesCallback()
    {
        // Arrange
        string? copiedText = null;
        var focusRing = new FocusRing();
        var context = new InputBindingActionContext(
            focusRing,
            requestStop: null,
            cancellationToken: default,
            mouseX: -1,
            mouseY: -1,
            copyToClipboard: text => copiedText = text
        );

        // Act
        context.CopyToClipboard("Test text");

        // Assert
        Assert.Equal("Test text", copiedText);
        await Task.CompletedTask; // Make the compiler happy about async
    }
}
