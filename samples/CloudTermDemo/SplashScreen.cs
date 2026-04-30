using System.Diagnostics;
using System.Reflection;
using Hex1b;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Animated splash screen that displays a fluid blue sky background with a
/// half-block cloud that fades in, title text, and version number.
/// Automatically transitions to the main screen after ~2.5 seconds.
/// </summary>
public sealed class SplashScreen
{
    private static readonly TimeSpan SplashDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan FadeOutStart = TimeSpan.FromSeconds(2.0);

    private static readonly string Hex1bVersion = GetHex1bVersion();

    private readonly AppState _appState;

    public SplashScreen(AppState appState)
    {
        _appState = appState;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        var elapsed = Stopwatch.GetElapsedTime(_appState.SplashStartTimestamp);
        var elapsedSeconds = elapsed.TotalSeconds;

        // Auto-transition to main screen
        if (elapsed >= SplashDuration)
        {
            _appState.CurrentScreen = AppScreen.Main;
            _appState.StatusMessage = "Ready";
            app.Invalidate();
        }

        // Cloud fade-in: ramp opacity from 0 to 1 over FadeInDuration
        var fadeInProgress = Math.Clamp(elapsedSeconds / FadeInDuration.TotalSeconds, 0, 1);
        var cloudOpacity = 1.0 - Math.Pow(1.0 - fadeInProgress, 3); // ease-out cubic

        // Fade-out dimming in the last 0.5 seconds
        var fadeOutProgress = elapsed >= FadeOutStart
            ? Math.Clamp(
                (elapsedSeconds - FadeOutStart.TotalSeconds) /
                (SplashDuration.TotalSeconds - FadeOutStart.TotalSeconds), 0, 1)
            : 0.0;

        return ctx.Surface(s =>
        {
            var layers = new List<Hex1b.Widgets.SurfaceLayer>
            {
                s.Layer(CloudEffects.FluidSkyBackground(elapsedSeconds, s.Height)),
                s.Layer(CloudEffects.HalfBlockCloud(cloudOpacity, s.Width, s.Height)),
                s.Layer(CloudEffects.TextOverlay(cloudOpacity, s.Width, s.Height, Hex1bVersion)),
            };

            if (fadeOutProgress > 0)
            {
                layers.Add(s.Layer(CloudEffects.FadeOut(fadeOutProgress)));
            }

            return layers;
        })
        .RedrawAfter(TimeSpan.FromMilliseconds(33))
        .Fill();
    }

    private static string GetHex1bVersion()
    {
        var hex1bAssembly = typeof(Hex1bTerminal).Assembly;
        var infoVersion = hex1bAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (infoVersion is not null)
        {
            // Strip source hash suffix if present (e.g. "1.2.3+abc123")
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var fileVersion = hex1bAssembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version;

        return fileVersion ?? "0.0.0";
    }
}
