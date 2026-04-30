using System.Diagnostics;
using Hex1b;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Animated splash screen that displays a swirling blue background with a cloud
/// logo that fades in. Automatically transitions to the main screen after ~2.5 seconds.
/// </summary>
public sealed class SplashScreen
{
    private static readonly TimeSpan SplashDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan FadeOutStart = TimeSpan.FromSeconds(2.0);

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
        // Ease out cubic for a nice deceleration
        var cloudOpacity = 1.0 - Math.Pow(1.0 - fadeInProgress, 3);

        // Fade-out dimming in the last 0.5 seconds
        var fadeOutProgress = elapsed >= FadeOutStart
            ? Math.Clamp((elapsedSeconds - FadeOutStart.TotalSeconds) / (SplashDuration.TotalSeconds - FadeOutStart.TotalSeconds), 0, 1)
            : 0.0;

        return ctx.Surface(s =>
        {
            var layers = new List<Hex1b.Widgets.SurfaceLayer>
            {
                s.Layer(CloudEffects.SwirlingBlueBackground(elapsedSeconds)),
                s.Layer(CloudEffects.CloudFadeIn(cloudOpacity, s.Width, s.Height)),
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
}
