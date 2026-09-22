using System.Text;
using System.Text.Json;

namespace Hex1b;

/// <summary>
/// Represents the playback state of an asciinema recording.
/// </summary>
public enum AsciinemaPlaybackState
{
    /// <summary>
    /// Playback has not started yet.
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// Playback is currently running.
    /// </summary>
    Playing,
    
    /// <summary>
    /// Playback is paused.
    /// </summary>
    Paused,
    
    /// <summary>
    /// Playback has completed (reached end of recording).
    /// </summary>
    Completed
}
