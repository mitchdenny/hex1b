namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    internal TimeProvider AutomationTimeProvider => _timeProvider;

    /// <summary>Reports whether automation resizing can be completed without remote producer confirmation.</summary>
    internal bool SupportsAutomationResize => _workload is not Hmp1WorkloadAdapter;

    /// <summary>
    /// Resizes locally and awaits workload/filter notification, propagating their failures.
    /// HMP1 geometry is rejected because this operation cannot confirm the producer's resize.
    /// </summary>
    internal async Task ResizeForAutomationAsync(int width, int height, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!SupportsAutomationResize)
            throw new NotSupportedException(
                "Awaited automation resizing requires producer confirmation and is not supported for HMP1 workloads.");

        Resize(width, height);
        await NotifyPresentationFiltersResizeAsync(width, height, ct).ConfigureAwait(false);
        await NotifyWorkloadFiltersResizeAsync(width, height, ct).ConfigureAwait(false);
        await _workload.ResizeAsync(width, height, ct).ConfigureAwait(false);
    }
}
