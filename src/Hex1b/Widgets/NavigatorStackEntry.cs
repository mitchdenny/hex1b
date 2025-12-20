using System.Diagnostics.CodeAnalysis;

namespace Hex1b.Widgets;

/// <summary>
/// Internal entry that pairs a route with its saved focus state.
/// </summary>
[Experimental("HEX1B001")]
internal class NavigatorStackEntry
{
    public NavigatorRoute Route { get; }
    public int SavedFocusIndex { get; set; } = 0;

    public NavigatorStackEntry(NavigatorRoute route)
    {
        Route = route;
    }
}
