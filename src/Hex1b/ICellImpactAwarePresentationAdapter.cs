using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Extended presentation adapter interface that receives cell impact information
/// instead of raw ANSI bytes.
/// </summary>
/// <remarks>
/// <para>
/// When a presentation adapter implements this interface, the terminal will call
/// <see cref="WriteOutputWithImpactsAsync"/> instead of <see cref="IHex1bTerminalPresentationAdapter.WriteOutputAsync"/>,
/// providing pre-parsed tokens with their cell impacts.
/// </para>
/// <para>
/// This is useful for presentation adapters that need to maintain a screen buffer
/// (like <see cref="TerminalWidgetHandle"/>) without duplicating ANSI parsing logic.
/// The cell impacts provide exactly which cells changed and their new state.
/// </para>
/// <para>
/// This acts as a "built-in filter" that is guaranteed to be at the end of the
/// presentation filter chain, receiving the fully processed token stream.
/// </para>
/// </remarks>
public interface ICellImpactAwarePresentationAdapter : IHex1bTerminalPresentationAdapter
{
    /// <summary>
    /// Called when output is being sent to the presentation layer, with cell impact information.
    /// </summary>
    /// <param name="appliedTokens">The applied tokens with their cell impacts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the output has been processed.</returns>
    ValueTask WriteOutputWithImpactsAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default);
}
