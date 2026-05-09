namespace Hex1bStory;

/// <summary>
/// Top-level mode of the presentation shell. The shell renders a different
/// view depending on which mode is active.
/// </summary>
internal enum PresenterMode
{
    /// <summary>Showing the playlist picker.</summary>
    Menu,

    /// <summary>Showing the slides of the active playlist.</summary>
    Presenting,
}
