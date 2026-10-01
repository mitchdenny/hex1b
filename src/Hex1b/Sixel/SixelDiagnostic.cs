using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// A single explicit Sixel parse diagnostic explaining a degraded or annotated outcome.
/// </summary>
/// <param name="Code">The specific reason this diagnostic was raised.</param>
/// <param name="Offset">The byte offset into the payload where the condition was observed.</param>
/// <param name="Command">The offending command byte, when applicable.</param>
/// <param name="Message">A human-readable explanation.</param>
public readonly record struct SixelDiagnostic(
    SixelDiagnosticCode Code,
    long Offset,
    byte? Command,
    string Message);
