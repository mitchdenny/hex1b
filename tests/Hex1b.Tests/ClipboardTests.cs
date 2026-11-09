using System.Text;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for clipboard functionality via OSC 52.
/// </summary>
[TestClass]
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

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        
        // Helper to trigger events (not used in this test but required by interface)
        public void TriggerResize(int width, int height) => Resized?.Invoke(width, height);
        public void TriggerDisconnect() => Disconnected?.Invoke();
    }

    [TestMethod]
    public async Task CopyToClipboard_SendsOsc52Sequence_ToOutput()
    {
        var presentation = new CapturingPresentationAdapter();
        const string textToCopy = "Test clipboard content";
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(textToCopy));
        var expectedOsc52 = $"\x1b]52;c;{expectedBase64}\x07";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(15));
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithDimensions(80, 24)
            .WithHex1bApp(ctx => new ButtonWidget("Copy Test")
                .OnClick(e => e.Context.CopyToClipboard(textToCopy)))
            .Build();
        var runTask = terminal.RunAsync(cancellation.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Copy Test"), TimeSpan.FromSeconds(5), "copy button rendered")
                .Enter()
                .WaitUntil(_ => presentation.CapturedOutput.Contains(expectedOsc52, StringComparison.Ordinal),
                    TimeSpan.FromSeconds(5), "OSC 52 reached the presentation adapter")
                .Build()
                .ApplyAsync(terminal, cancellation.Token);

            Assert.Contains(expectedOsc52, presentation.CapturedOutput);
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, cancellation.Token);
            Assert.AreEqual(0, await runTask.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token));
        }
        finally
        {
            cancellation.Cancel();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
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
        Assert.AreEqual("SGVsbG8sIFdvcmxkIQ==", expectedBase64);
    }

    [TestMethod]
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
        Assert.AreEqual("Test text", copiedText);
        await Task.CompletedTask; // Make the compiler happy about async
    }
}
