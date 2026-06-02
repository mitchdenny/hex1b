using Hex1b;
using Hex1b.Input;
using Hex1b.Integrations.Spectre.SpectreConsole;

namespace Hex1b.Integrations.Spectre.Tests;

[TestClass]
public class Hex1bAnsiConsoleInputTests
{
    [TestMethod]
    public void IsKeyAvailable_WithEmptyChannel_ReturnsFalse()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        Assert.IsFalse(input.IsKeyAvailable());
    }

    [TestMethod]
    public async Task IsKeyAvailable_AfterWritingKey_ReturnsTrue()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        await adapter.WriteInputEventAsync(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));

        Assert.IsTrue(input.IsKeyAvailable());
    }

    [TestMethod]
    public async Task IsKeyAvailable_WithOnlyResizeEvent_DrainsItAndReturnsFalse()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        await adapter.WriteInputEventAsync(new Hex1bResizeEvent(120, 30));

        Assert.IsFalse(input.IsKeyAvailable());
    }

    [TestMethod]
    public async Task ReadKey_WithKeyInChannel_ReturnsConsoleKeyInfo()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        await adapter.WriteInputEventAsync(
            new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

        var result = input.ReadKey(intercept: true);

        Assert.IsNotNull(result);
        Assert.AreEqual(ConsoleKey.Enter, result!.Value.Key);
    }

    [TestMethod]
    public async Task ReadKey_WithModifiers_PreservesModifierFlags()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        await adapter.WriteInputEventAsync(
            new Hex1bKeyEvent(Hex1bKey.C, '\x03', Hex1bModifiers.Control));

        var result = input.ReadKey(intercept: true);

        Assert.IsNotNull(result);
        Assert.AreEqual(ConsoleKey.C, result!.Value.Key);
        Assert.AreEqual(ConsoleModifiers.Control, result.Value.Modifiers);
    }

    [TestMethod]
    public async Task ReadKey_DiscardsNonKeyEvents()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        await adapter.WriteInputEventAsync(new Hex1bResizeEvent(80, 24));
        await adapter.WriteInputEventAsync(
            new Hex1bKeyEvent(Hex1bKey.Q, 'q', Hex1bModifiers.None));

        var result = input.ReadKey(intercept: true);

        Assert.IsNotNull(result);
        Assert.AreEqual(ConsoleKey.Q, result!.Value.Key);
        Assert.AreEqual('q', result.Value.KeyChar);
    }

    [TestMethod]
    public async Task ReadKeyAsync_AwaitsUntilKeyArrives()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);

        var readTask = input.ReadKeyAsync(intercept: true, CancellationToken.None);
        Assert.IsFalse(readTask.IsCompleted);

        await adapter.WriteInputEventAsync(
            new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None));

        var result = await readTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsNotNull(result);
        Assert.AreEqual(ConsoleKey.Spacebar, result!.Value.Key);
    }

    [TestMethod]
    public async Task ReadKeyAsync_RespectsCancellation()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var input = new Hex1bAnsiConsoleInput(adapter);
        using var cts = new CancellationTokenSource();

        var readTask = input.ReadKeyAsync(intercept: true, cts.Token);
        cts.Cancel();

        var ex = await TestSeq.RecordExceptionAsync(
            () => readTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsInstanceOfType<OperationCanceledException>(ex);
    }
}
