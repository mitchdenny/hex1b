namespace Hex1b.Tokens;

/// <summary>
/// Base type for all ANSI terminal tokens.
/// </summary>
/// <remarks>
/// <para>
/// Tokens represent parsed ANSI escape sequences and text content.
/// They are the intermediate representation between raw bytes from a workload
/// and the final bytes sent to a presentation adapter.
/// </para>
/// <para>
/// The token hierarchy is designed to:
/// <list type="bullet">
///   <item>Preserve enough information to serialize back to ANSI bytes</item>
///   <item>Be immutable (all types are records)</item>
///   <item>Support pattern matching in filter implementations</item>
/// </list>
/// </para>
/// </remarks>
public abstract record AnsiToken;
