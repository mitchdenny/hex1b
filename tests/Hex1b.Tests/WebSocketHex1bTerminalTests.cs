using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Xunit;

namespace Hex1b.Tests;

public class WebSocketHex1bTerminalTests
{
    [Fact]
    public void Constructor_SetsDefaultDimensions()
    {
        // Arrange & Act
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Assert
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public void Constructor_SetsCustomDimensions()
    {
        // Arrange & Act
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 120, 40);

        // Assert
        Assert.Equal(120, terminal.Width);
        Assert.Equal(40, terminal.Height);
    }

    [Fact]
    public void Resize_UpdatesDimensions()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 80, 24);

        // Act
        terminal.Resize(132, 50);

        // Assert
        Assert.Equal(132, terminal.Width);
        Assert.Equal(50, terminal.Height);
    }

    [Fact]
    public void Resize_RaisesOnResizeEvent()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 80, 24);
        int? receivedCols = null;
        int? receivedRows = null;
        terminal.OnResize += (cols, rows) =>
        {
            receivedCols = cols;
            receivedRows = rows;
        };

        // Act
        terminal.Resize(100, 30);

        // Assert
        Assert.Equal(100, receivedCols);
        Assert.Equal(30, receivedRows);
    }

    [Fact]
    public async Task Resize_PushesResizeInputEvent()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, 80, 24);

        // Act
        terminal.Resize(120, 40);

        // Assert - check that a Hex1bResizeEvent was pushed to the channel
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var inputEvent = await terminal.InputEvents.ReadAsync(cts.Token);
        
        var resizeEvent = Assert.IsType<Hex1bResizeEvent>(inputEvent);
        Assert.Equal(120, resizeEvent.Width);
        Assert.Equal(40, resizeEvent.Height);
    }

    [Fact]
    public void Write_SendsTextToWebSocket()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.Write("Hello, World!");

        // Assert - give a moment for the async send
        Thread.Sleep(50);
        Assert.Contains("Hello, World!", mockWebSocket.SentData);
    }

    [Fact]
    public void Clear_SendsClearSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.Clear();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[2J\x1b[H", mockWebSocket.SentData);
    }

    [Fact]
    public void SetCursorPosition_SendsAnsiSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.SetCursorPosition(10, 5);

        // Assert
        Thread.Sleep(50);
        // ANSI position is 1-based, so (10, 5) becomes row 6, col 11
        Assert.Contains("\x1b[6;11H", mockWebSocket.SentData);
    }

    [Fact]
    public void EnterAlternateScreen_SendsCorrectSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.EnterAlternateScreen();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[?1049h", mockWebSocket.SentData);
    }

    [Fact]
    public void ExitAlternateScreen_SendsCorrectSequence()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);

        // Act
        terminal.ExitAlternateScreen();

        // Assert
        Thread.Sleep(50);
        Assert.Contains("\x1b[?1049l", mockWebSocket.SentData);
    }

    [Theory]
    [InlineData("\x1b[A", Hex1bKey.UpArrow, Hex1bModifiers.None)]
    [InlineData("\x1b[B", Hex1bKey.DownArrow, Hex1bModifiers.None)]
    [InlineData("\x1b[C", Hex1bKey.RightArrow, Hex1bModifiers.None)]
    [InlineData("\x1b[D", Hex1bKey.LeftArrow, Hex1bModifiers.None)]
    [InlineData("\x1b[1;2A", Hex1bKey.UpArrow, Hex1bModifiers.Shift)]    // Shift+Up
    [InlineData("\x1b[1;2B", Hex1bKey.DownArrow, Hex1bModifiers.Shift)]  // Shift+Down
    [InlineData("\x1b[1;2C", Hex1bKey.RightArrow, Hex1bModifiers.Shift)] // Shift+Right
    [InlineData("\x1b[1;2D", Hex1bKey.LeftArrow, Hex1bModifiers.Shift)]  // Shift+Left
    [InlineData("\x1b[1;3C", Hex1bKey.RightArrow, Hex1bModifiers.Alt)] // Alt+Right
    [InlineData("\x1b[1;5C", Hex1bKey.RightArrow, Hex1bModifiers.Control)] // Ctrl+Right
    [InlineData("\x1b[1;6C", Hex1bKey.RightArrow, Hex1bModifiers.Shift | Hex1bModifiers.Control)]  // Shift+Ctrl+Right
    [InlineData("\x1b[H", Hex1bKey.Home, Hex1bModifiers.None)]
    [InlineData("\x1b[F", Hex1bKey.End, Hex1bModifiers.None)]
    [InlineData("\x1b[1;2H", Hex1bKey.Home, Hex1bModifiers.Shift)]       // Shift+Home
    [InlineData("\x1b[1;2F", Hex1bKey.End, Hex1bModifiers.Shift)]        // Shift+End
    public async Task ProcessInputAsync_ParsesAnsiSequences(string sequence, Hex1bKey expectedKey, Hex1bModifiers expectedModifiers)
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        mockWebSocket.QueueMessage(sequence);
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act
        var processTask = terminal.ProcessInputAsync(cts.Token);
        
        Hex1bKeyEvent? receivedEvent = null;
        try
        {
            receivedEvent = await terminal.InputEvents.ReadAsync(cts.Token) as Hex1bKeyEvent;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(expectedKey, receivedEvent.Key);
        Assert.Equal(expectedModifiers, receivedEvent.Modifiers);
    }

    [Theory]
    [InlineData("\x1b[3~", Hex1bKey.Delete)]
    [InlineData("\x1b[5~", Hex1bKey.PageUp)]
    [InlineData("\x1b[6~", Hex1bKey.PageDown)]
    public async Task ProcessInputAsync_ParsesTildeSequences(string sequence, Hex1bKey expectedKey)
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        mockWebSocket.QueueMessage(sequence);
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act
        var processTask = terminal.ProcessInputAsync(cts.Token);
        
        Hex1bKeyEvent? receivedEvent = null;
        try
        {
            receivedEvent = await terminal.InputEvents.ReadAsync(cts.Token) as Hex1bKeyEvent;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(expectedKey, receivedEvent.Key);
    }

    [Theory]
    [InlineData("\x1b[<0;10;5M", MouseButton.Left, MouseAction.Down, 9, 4)]  // Left click at (10,5) -> 0-based (9,4)
    [InlineData("\x1b[<2;20;10M", MouseButton.Right, MouseAction.Down, 19, 9)]  // Right click
    [InlineData("\x1b[<35;15;8M", MouseButton.None, MouseAction.Move, 14, 7)]  // Mouse move (35 = 32 + 3)
    [InlineData("\x1b[<0;5;5m", MouseButton.Left, MouseAction.Up, 4, 4)]  // Left release (lowercase m)
    public async Task ProcessInputAsync_ParsesMouseEvents(string sequence, MouseButton expectedButton, MouseAction expectedAction, int expectedX, int expectedY)
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        mockWebSocket.QueueMessage(sequence);
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, enableMouse: true);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act
        var processTask = terminal.ProcessInputAsync(cts.Token);
        
        Hex1bMouseEvent? receivedEvent = null;
        try
        {
            receivedEvent = await terminal.InputEvents.ReadAsync(cts.Token) as Hex1bMouseEvent;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(expectedButton, receivedEvent.Button);
        Assert.Equal(expectedAction, receivedEvent.Action);
        Assert.Equal(expectedX, receivedEvent.X);
        Assert.Equal(expectedY, receivedEvent.Y);
    }

    [Fact]
    public void EnterAlternateScreen_WithMouse_SendsMouseTrackingEscapes()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, enableMouse: true);

        // Act
        terminal.EnterAlternateScreen();

        // Assert - should contain mouse tracking enable sequences
        var sent = mockWebSocket.SentData;
        Assert.Contains("\x1b[?1000h", sent);  // Enable mouse button events
        Assert.Contains("\x1b[?1003h", sent);  // Enable all motion events
        Assert.Contains("\x1b[?1006h", sent);  // Enable SGR extended mode
    }

    [Fact]
    public void ExitAlternateScreen_WithMouse_SendsMouseTrackingDisableEscapes()
    {
        // Arrange
        using var mockWebSocket = new MockWebSocket();
        using var terminal = new WebSocketHex1bTerminal(mockWebSocket, enableMouse: true);

        // Act
        terminal.ExitAlternateScreen();

        // Assert - should contain mouse tracking disable sequences
        var sent = mockWebSocket.SentData;
        Assert.Contains("\x1b[?1000l", sent);  // Disable mouse button events
        Assert.Contains("\x1b[?1003l", sent);  // Disable all motion events
        Assert.Contains("\x1b[?1006l", sent);  // Disable SGR extended mode
    }

    /// <summary>
    /// Mock WebSocket for testing that captures sent data and can queue messages to receive.
    /// </summary>
    private class MockWebSocket : WebSocket
    {
        private readonly StringBuilder _sentData = new();
        private readonly Channel<string> _receiveQueue = Channel.CreateUnbounded<string>();
        private WebSocketState _state = WebSocketState.Open;

        public string SentData => _sentData.ToString();

        public void QueueMessage(string message) => _receiveQueue.Writer.TryWrite(message);

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public override void Abort() => _state = WebSocketState.Aborted;

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose() => _state = WebSocketState.Closed;

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_receiveQueue.Reader.TryRead(out var message))
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
                return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
            }
            
            // Wait for a message or cancellation
            try
            {
                var msg = await _receiveQueue.Reader.ReadAsync(cancellationToken);
                var bytes = Encoding.UTF8.GetBytes(msg);
                Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
                return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
            }
            catch (OperationCanceledException)
            {
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (_state != WebSocketState.Open)
                return Task.CompletedTask;

            var text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            _sentData.Append(text);
            return Task.CompletedTask;
        }
    }
}
