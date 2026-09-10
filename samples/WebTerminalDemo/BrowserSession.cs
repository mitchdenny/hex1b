using System.Net.WebSockets;
using System.Text.Json;
using Hex1b;
using Microsoft.Extensions.Logging;

namespace WebTerminalDemo;

internal sealed class BrowserSession(WebSocket socket, TerminalView view, string? name, bool relay, ILogger logger,
    InitialViewFailure initialFailure = InitialViewFailure.None)
{
    private TerminalInstance Instance => view.Instance;

    public async Task RunAsync(CancellationToken requestAborted)
    {
        using var operations = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, Instance.Stopping);
        using var transport = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopRegistration = operations.Token.Register(() => stopped.TrySetResult());
        Hwt1PresentationAdapter? presentation = null;
        Hmp1BrowserView? relayView = null;
        Task setup = Task.CompletedTask, sender = Task.CompletedTask, receiver = Task.CompletedTask;
        var close = new BrowserCloseRequest(WebSocketCloseStatus.NormalClosure, "View closed");
        try
        {
            if (initialFailure is InitialViewFailure.BeforeFrameClose or InitialViewFailure.BeforeFrameAbort)
            {
                close = new(WebSocketCloseStatus.NormalClosure, "Demo: closed before first frame",
                    initialFailure == InitialViewFailure.BeforeFrameAbort);
                return;
            }

            setup = CreatePresentationAsync();
            var started = await Task.WhenAny(setup, view.Failure, stopped.Task);
            if (started != setup)
            {
                if (started == view.Failure)
                    close = await view.Failure;
                return;
            }
            await setup;
            sender = SendFramesAsync(presentation!, operations.Token, transport.Token);
            receiver = ReceiveAsync(presentation!, operations.Token, transport.Token);
            var finished = await Task.WhenAny(sender, receiver, view.Failure, stopped.Task);
            if (finished == view.Failure)
                close = await view.Failure;
            else
                await finished;
        }
        catch (OperationCanceledException) when (operations.IsCancellationRequested) { }
        catch (WebSocketException ex)
        {
            logger.LogInformation("Browser view of {Instance} disconnected: {Reason}", Instance.Id, ex.Message);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Browser view of {Instance} stopped acknowledging frames", Instance.Id);
            close = new(WebSocketCloseStatus.PolicyViolation, "Frame acknowledgement timed out");
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException or ArgumentException)
        {
            logger.LogWarning(ex, "Invalid browser input or state for {Instance}", Instance.Id);
            close = new(WebSocketCloseStatus.PolicyViolation, "Invalid input or terminal state; see server log");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Browser view of {Instance} failed", Instance.Id);
            close = new(WebSocketCloseStatus.InternalServerError, "Terminal view failed; see server log");
        }
        finally
        {
            view.BeginClosing();
            close = Instance.CloseReason ?? close;
            // Cancelling ReceiveAsync (or a socket write) aborts ManagedWebSocket.
            // Stop producer work first, but keep the transport alive for the close handshake.
            await operations.CancelAsync();
            await ObserveAsync(setup);
            if (close.Abort)
                socket.Abort();
            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                // A close frame is a write too: never overlap it with an in-flight frame.
                await ObserveAsync(sender).WaitAsync(closeTimeout.Token);
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    if (receiver.IsCompleted && socket.State == WebSocketState.Open)
                    {
                        await ObserveAsync(receiver);
                        receiver = DrainCloseAsync(transport.Token);
                    }
                    await socket.CloseOutputAsync(close.Status, close.Reason, closeTimeout.Token);
                    await ObserveAsync(receiver).WaitAsync(closeTimeout.Token);
                    if (socket.State == WebSocketState.CloseSent)
                    {
                        receiver = DrainCloseAsync(transport.Token);
                        await receiver.WaitAsync(closeTimeout.Token);
                    }
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "Browser close handshake failed for {Instance}", Instance.Id);
                socket.Abort();
            }
            finally
            {
                await transport.CancelAsync();
                await ObserveAsync(sender);
                await ObserveAsync(receiver);
                if (presentation is not null)
                {
                    try
                    {
                        if (relayView is not null)
                            await relayView.DisposeAsync();
                        else
                            await presentation.DisposeAsync();
                    }
                    catch (Exception ex) { logger.LogDebug(ex, "HMP browser peer disposal failed for {Instance}", Instance.Id); }
                }
            }
        }

        async Task CreatePresentationAsync()
        {
            if (relay)
            {
                relayView = await Hmp1BrowserView.CreateAsync(Instance.Presentation, name, operations.Token);
                presentation = relayView.Presentation;
            }
            else
                presentation = await Instance.Presentation.CreateBrowserViewAsync(name, operations.Token);
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogDebug(ex, "Browser task ended for {Instance}", Instance.Id); }
    }

    private async Task SendFramesAsync(Hwt1PresentationAdapter presentation, CancellationToken readToken,
        CancellationToken transportToken)
    {
        while (true)
        {
            var frame = await presentation.ReadFrameAsync(readToken);
            readToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(transportToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try { await socket.SendAsync(frame, WebSocketMessageType.Binary, true, timeout.Token); }
            catch (OperationCanceledException) when (!transportToken.IsCancellationRequested)
            {
                throw new TimeoutException("Browser did not receive a frame within two minutes.");
            }
        }
    }

    private async Task ReceiveAsync(Hwt1PresentationAdapter presentation, CancellationToken operationToken,
        CancellationToken transportToken)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                if (length == buffer.Length)
                    throw new InvalidDataException("Input exceeds 64 KiB.");
                result = await socket.ReceiveAsync(buffer.AsMemory(length), transportToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                if (operationToken.IsCancellationRequested)
                {
                    await DrainCloseAsync(transportToken);
                    return;
                }
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException("Expected a JSON command.");
                length += result.Count;
            } while (!result.EndOfMessage);

            try { await presentation.HandleMessageAsync(buffer.AsMemory(0, length), operationToken); }
            catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
            {
                await DrainCloseAsync(transportToken);
                return;
            }
        }
    }

    private async Task DrainCloseAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (socket.State is WebSocketState.Open or WebSocketState.CloseSent)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
        }
    }
}
