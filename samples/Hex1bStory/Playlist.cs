namespace Hex1bStory;

/// <summary>
/// A named, ordered collection of slides. The presenter picks one of these
/// from the menu when the app starts.
/// </summary>
/// <param name="Name">Short identifier shown in the picker and the footer.</param>
/// <param name="Description">Single-line description shown next to the name on the menu.</param>
/// <param name="Slides">Slides shown in order. Must contain at least one entry.</param>
internal sealed record Playlist(string Name, string Description, IReadOnlyList<ISlide> Slides);
