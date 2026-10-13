namespace Hex1b;

/// <summary>Captures the complete last report for one OSC 7501 program id.</summary>
/// <param name="Id">The hierarchical id; an empty string identifies the root.</param>
/// <param name="State">The program's state.</param>
/// <param name="Kind">The action required when blocked, if reported.</param>
/// <param name="Progress">The percentage from 0 to 100 when working or blocked, or null for indeterminate progress.</param>
/// <param name="App">The program identifier explicitly reported on this record.</param>
/// <param name="Title">The decoded single-line label, if reported.</param>
/// <param name="Message">The decoded single-line status message, if reported.</param>
/// <param name="EffectiveApp">The explicit app or the nearest ancestor's app at snapshot time.</param>
/// <remarks>Each report completely replaces its record. Missing values are not retained from prior reports.</remarks>
public sealed record TerminalProgramStatusRecord(
    string Id,
    TerminalProgramStatusState State,
    TerminalProgramStatusKind? Kind = null,
    int? Progress = null,
    string? App = null,
    string? Title = null,
    string? Message = null,
    string? EffectiveApp = null);
