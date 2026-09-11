namespace Hex1b;

// Keeps ordered HMP control information intact through internal workload wrappers.
internal interface IHmp1TerminalOutputSource
{
    Hmp1WorkloadAdapter? Hmp1Workload { get; }
    ValueTask<Hmp1WorkloadOutput> ReadTerminalOutputAsync(CancellationToken cancellationToken);
}
