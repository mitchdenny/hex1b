namespace Hex1b.Input;

/// <summary>
/// Base class for all Hex1b events. Events are categorized into:
/// - Keyboard events: Routed to focused node via InputRouter
/// - System events: Handled by app (resize, etc.)
/// - Terminal events: Handled by app for terminal capability detection
/// </summary>
public abstract record Hex1bEvent;
