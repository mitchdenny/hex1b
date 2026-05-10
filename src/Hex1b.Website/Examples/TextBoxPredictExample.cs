using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class TextBoxPredictExample(ILogger<TextBoxPredictExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBoxPredictExample> _logger = logger;

    public override string Id => "textbox-predict";
    public override string Title => "TextBox - Inline Prediction";
    public override string Description =>
        "Inline predictive completions appear in dim text after the cursor. " +
        "Press Right Arrow to accept the suggestion or Escape to dismiss it.";

    private static readonly string[] Phrases =
    [
        "the quick brown fox jumps over the lazy dog",
        "hello world",
        "hex1b is a terminal ui library for .net",
        "predictive text saves keystrokes",
        "type any letter to see suggestions appear",
    ];

    private class PredictState
    {
        public string Input { get; set; } = "";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating textbox predict example");
        var state = new PredictState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Predictive TextBox"),
                v.Text("──────────────────"),
                v.Text(""),
                v.Text("Start typing — the predictor returns the longest dictionary"),
                v.Text("entry whose prefix matches what you've typed."),
                v.Text(""),
                v.TextBox(state.Input)
                    .OnTextChanged(args => state.Input = args.NewText)
                    .Predict(async (text, ct) =>
                    {
                        // Synthesize an async hop — a real predictor might
                        // await an HTTP/LLM call here.
                        await Task.Yield();

                        if (string.IsNullOrEmpty(text)) return null;

                        string? best = null;
                        foreach (var entry in Phrases)
                        {
                            if (entry.Length <= text.Length) continue;
                            if (!entry.StartsWith(text, StringComparison.OrdinalIgnoreCase)) continue;
                            if (best is null || entry.Length > best.Length) best = entry;
                        }
                        return best is null ? null : best[text.Length..];
                    }),
                v.Text(""),
                v.Text("Right arrow accepts the suggestion. Escape dismisses it.")
            ]);
        };
    }
}
