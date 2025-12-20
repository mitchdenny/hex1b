using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a screen/page that can be displayed in a Navigator.
/// </summary>
/// <param name="Id">Unique identifier for this route.</param>
/// <param name="Builder">Function that builds the widget for this route, given the navigator for sub-navigation.</param>
[Experimental("HEX1B001")]
public record NavigatorRoute(string Id, Func<NavigatorState, Hex1bWidget> Builder);
