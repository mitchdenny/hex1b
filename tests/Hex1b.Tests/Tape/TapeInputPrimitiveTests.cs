using System.Text;
using Hex1b.Automation;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class TapeInputPrimitiveTests
{
    [TestMethod]
    public async Task Type_SupplementaryCharacters_PreservesUtf8Input()
    {
        var workload = new InputCollector();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 4).Build();

        using var result = await new Hex1bTerminalInputSequenceBuilder()
            .Type("A\U0001F680e\u0301").Build().ApplyAsync(terminal);

        Assert.AreEqual("A\U0001F680e\u0301", workload.Text.ToString());
    }

    [TestMethod]
    public async Task Wait_WithTimeProvider_CancellationCompletesWithoutAdvancingClock()
    {
        var clock = new FakeTimeProvider();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new InputCollector()).WithHeadless().WithDimensions(20, 4).Build();
        using var cancellation = new CancellationTokenSource();
        var playback = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(new Hex1bTerminalInputSequenceOptions { TimeProvider = clock })
            .Wait(TimeSpan.FromHours(1)).Build().ApplyAsync(terminal, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await playback.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class InputCollector : IHex1bTerminalWorkloadAdapter
    {
        internal StringBuilder Text { get; } = new();
        public event Action? Disconnected { add { } remove { } }
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Text.Append(Encoding.UTF8.GetString(data.Span));
            return ValueTask.CompletedTask;
        }
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
