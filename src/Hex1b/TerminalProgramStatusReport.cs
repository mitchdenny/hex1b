namespace Hex1b;

/// <summary>
/// Describes a complete replacement of an OSC 7501 program-status record.
/// </summary>
/// <param name="State">The program's current state.</param>
/// <remarks>
/// Omitted optional fields remove their previous values, including the application label.
/// Returning from a callback does not automatically change or clear program status.
/// </remarks>
public sealed record TerminalProgramStatusReport(TerminalProgramStatusState State)
{
    /// <summary>Gets the record identifier. Null or empty addresses the root record.</summary>
    /// <remarks>
    /// Identifiers contain up to eight slash-separated ASCII segments, each at most 32
    /// characters, with at most 128 characters overall. Segments accept letters, digits,
    /// underscores, periods, plus signs, and hyphens.
    /// </remarks>
    public string? Id { get; init; }

    /// <summary>Gets the application label for this replacement.</summary>
    /// <remarks>Labels use the identifier segment character set and contain 1 to 32 characters.</remarks>
    public string? App { get; init; }

    /// <summary>Gets the reason that the program is blocked.</summary>
    /// <remarks>Only valid for <see cref="TerminalProgramStatusState.Blocked"/> reports.</remarks>
    public TerminalProgramStatusKind? Kind { get; init; }

    /// <summary>Gets the progress percentage, from 0 to 100 inclusive.</summary>
    /// <remarks>Only valid for working or blocked reports.</remarks>
    public int? Progress { get; init; }

    /// <summary>Gets the short human-readable title.</summary>
    /// <remarks>At most 192 UTF-8 bytes; control characters and invalid Unicode are rejected.</remarks>
    public string? Title { get; init; }

    /// <summary>Gets the human-readable message.</summary>
    /// <remarks>At most 2048 UTF-8 bytes; control characters and invalid Unicode are rejected.</remarks>
    public string? Message { get; init; }
}
