namespace Hex1b.Widgets;

/// <summary>
/// Marker interface for container widgets that support floating children.
/// When a container implements this interface, the <c>Float()</c> extension
/// method becomes available in its widget context, allowing children to
/// opt out of the normal layout flow.
/// </summary>
public interface IFloatWidgetContainer;
