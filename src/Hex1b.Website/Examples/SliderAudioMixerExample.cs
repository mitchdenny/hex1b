using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Slider Widget Documentation: Audio Mixer Example
/// Demonstrates multiple sliders in a form layout.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the audioMixerCode sample in:
/// src/content/guide/widgets/slider.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SliderAudioMixerExample(ILogger<SliderAudioMixerExample> logger) : Hex1bExample
{
    private readonly ILogger<SliderAudioMixerExample> _logger = logger;

    public override string Id => "slider-audio-mixer";
    public override string Title => "Slider Widget - Audio Mixer";
    public override string Description => "Demonstrates multiple sliders in a form layout";

    private double _master = 80.0;
    private double _music = 60.0;
    private double _effects = 90.0;

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating slider audio mixer example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Audio Settings"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text($"Master:  {_master,3:F0}% "),
                        h.Slider(80).OnValueChanged(e => _master = e.Value).Fill()
                    ]),
                    v.HStack(h => [
                        h.Text($"Music:   {_music,3:F0}% "),
                        h.Slider(60).OnValueChanged(e => _music = e.Value).Fill()
                    ]),
                    v.HStack(h => [
                        h.Text($"Effects: {_effects,3:F0}% "),
                        h.Slider(90).OnValueChanged(e => _effects = e.Value).Fill()
                    ]),
                    v.Text(""),
                    v.Text("Tab to switch, arrows to adjust")
                ])
            ], title: "Settings");
        };
    }
}
