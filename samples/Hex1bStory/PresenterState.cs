namespace Hex1bStory;

/// <summary>
/// Mutable shell state for the presentation app. Owned by <c>Program.cs</c>
/// for the lifetime of the process and consulted on every render.
/// </summary>
internal sealed class PresenterState
{
    /// <summary>Which top-level view the shell should render right now.</summary>
    public PresenterMode Mode { get; private set; } = PresenterMode.Menu;

    /// <summary>The active playlist while in <see cref="PresenterMode.Presenting"/>; null in Menu mode.</summary>
    public Playlist? Current { get; private set; }

    /// <summary>Zero-based index into <see cref="Current"/>'s slides.</summary>
    public int SlideIndex { get; private set; }

    /// <summary>Switch into presenting mode at the start of a playlist.</summary>
    public void StartPlaylist(Playlist playlist)
    {
        Current = playlist;
        SlideIndex = 0;
        Mode = PresenterMode.Presenting;
    }

    /// <summary>Return to the playlist picker.</summary>
    public void ReturnToMenu()
    {
        Current = null;
        SlideIndex = 0;
        Mode = PresenterMode.Menu;
    }

    /// <summary>Advance to the next slide; clamped to the last slide.</summary>
    public void NextSlide()
    {
        if (Current is null) return;
        if (SlideIndex < Current.Slides.Count - 1)
        {
            SlideIndex++;
        }
    }

    /// <summary>Go back one slide; clamped to the first slide.</summary>
    public void PreviousSlide()
    {
        if (Current is null) return;
        if (SlideIndex > 0)
        {
            SlideIndex--;
        }
    }

    /// <summary>Jump to the first slide.</summary>
    public void FirstSlide()
    {
        if (Current is null) return;
        SlideIndex = 0;
    }

    /// <summary>Jump to the last slide.</summary>
    public void LastSlide()
    {
        if (Current is null) return;
        SlideIndex = Current.Slides.Count - 1;
    }
}
