using AnimatedHumanoidDemo;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatedHumanoidDemo.Tests;

[TestClass]
public class HumanoidDemoTests
{
    [TestMethod]
    [DataRow(80, 24)]
    [DataRow(120, 40)]
    public async Task Build_AnimationAndViewControls_RenderAndRespond(int width, int height)
    {
        var demo = new HumanoidDemo(new AnimatedHumanoid(AnimatedHumanoid.AssetPath));
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => demo.Build(context))
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var run = terminal.RunAsync(cts.Token);
        try
        {
            using var initial = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Idle | playing")
                    && s.GetLineTrimmed(height - 3).Contains("6 Jump")
                    && s.GetLineTrimmed(height - 2).Contains("Quit")
                    && CountSceneGlyphs(s, height, false) > 40,
                    TimeSpan.FromSeconds(10), "robot and bottom controls rendered")
                .Build().ApplyAsync(terminal, cts.Token);

            using var walking = await new Hex1bTerminalInputSequenceBuilder()
                .Tab().Enter()
                .WaitUntil(s => s.ContainsText("Walking | playing"), TimeSpan.FromSeconds(5), "Walk button selects authored animation")
                .Build().ApplyAsync(terminal, cts.Token);
            var walkingImage = SceneText(walking, height);

            var sequence = new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Walking | playing") && SceneText(s, height) != walkingImage,
                    TimeSpan.FromSeconds(5), "scene visibly animates without further input")
                .Key(Hex1bKey.D3)
                .WaitUntil(s => s.ContainsText("Running | playing"), TimeSpan.FromSeconds(5), "Run shortcut")
                .Key(Hex1bKey.D4)
                .WaitUntil(s => s.ContainsText("Dance | playing"), TimeSpan.FromSeconds(5), "Dance shortcut")
                .Key(Hex1bKey.Spacebar)
                .WaitUntil(s => s.ContainsText("Dance | paused") && s.ContainsText("Resume"),
                    TimeSpan.FromSeconds(5), "paused playback")
                .Key(Hex1bKey.W)
                .WaitUntil(s => s.ContainsText("paused | wire") && CountSceneGlyphs(s, height, true) > 40,
                    TimeSpan.FromSeconds(5), "wireframe robot")
                .Key(Hex1bKey.W)
                .WaitUntil(s => s.ContainsText("paused | solid") && CountSceneGlyphs(s, height, false) > 40,
                    TimeSpan.FromSeconds(5), "solid robot")
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("Wave | playing"), TimeSpan.FromSeconds(5), "Wave resumes playback")
                .Key(Hex1bKey.D6)
                .WaitUntil(s => s.ContainsText("Jump | playing"), TimeSpan.FromSeconds(5), "Jump shortcut")
                .Key(Hex1bKey.D1)
                .WaitUntil(s => s.ContainsText("Idle | playing"), TimeSpan.FromSeconds(5), "return to Idle")
                .Ctrl().Key(Hex1bKey.C)
                .Build();
            using var final = await sequence.ApplyAsync(terminal, cts.Token);
            await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        finally
        {
            await cts.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static string SceneText(Hex1bTerminalSnapshot snapshot, int height)
        => string.Join('\n', Enumerable.Range(2, height - 7).Select(snapshot.GetLineTrimmed));

    private static int CountSceneGlyphs(Hex1bTerminalSnapshot snapshot, int height, bool wireframe)
    {
        var count = 0;
        for (var y = 2; y < height - 5; y++)
        {
            for (var x = 1; x < snapshot.Width - 1; x++)
            {
                var text = snapshot.GetCell(x, y).Character;
                if (text.Length > 0 && (wireframe
                    ? text[0] is >= '\u2801' and <= '\u28ff'
                    : text[0] is '\u2580' or '\u2584' or '\u2588'))
                    count++;
            }
        }
        return count;
    }
}
