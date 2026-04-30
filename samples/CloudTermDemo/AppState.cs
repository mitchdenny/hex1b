using System.Diagnostics;

namespace CloudTermDemo;

/// <summary>
/// Which screen the application is currently displaying.
/// </summary>
public enum AppScreen
{
    Splash,
    Main,
}

/// <summary>
/// Shared application state resolved via DI as a singleton.
/// Screens mutate this and call <c>app.Invalidate()</c> to trigger a re-render.
/// </summary>
public sealed class AppState
{
    public AppScreen CurrentScreen { get; set; } = AppScreen.Splash;

    /// <summary>
    /// Timestamp when the splash screen was first rendered (for animation timing).
    /// </summary>
    public long SplashStartTimestamp { get; set; } = Stopwatch.GetTimestamp();

    public string StatusMessage { get; set; } = "Initializing...";
}
