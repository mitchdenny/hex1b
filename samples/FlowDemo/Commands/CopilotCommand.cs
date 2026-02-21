using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the "flowdemo copilot" command — a mock Copilot CLI chat interface.
/// Each prompt/response pair is yielded as frozen output that scrolls up naturally.
/// Type /exit to end the session. Shift+Tab cycles between modes.
/// </summary>
internal static class CopilotCommand
{
    private enum Mode { Normal, Plan, Autopilot }

    private static readonly Mode[] Modes = [Mode.Normal, Mode.Plan, Mode.Autopilot];

    private static string ModeLabel(Mode mode) => mode switch
    {
        Mode.Normal => "normal",
        Mode.Plan => "plan",
        Mode.Autopilot => "autopilot",
        _ => "normal",
    };

    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;
        var currentMode = Mode.Normal;

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                while (true)
                {
                    string? submittedText = null;

                    await flow.SliceAsync(
                        builder: ctx => ctx.VStack(v =>
                        [
                            v.VStack(_ => []).Fill(),
                            v.Separator(),
                            v.TextBox().OnSubmit(e =>
                            {
                                submittedText = e.Text;
                                e.Context.RequestStop();
                            })
                            .WithInputBindings(bindings =>
                            {
                                bindings.Shift().Key(Hex1bKey.Tab).Action(actionCtx =>
                                {
                                    int idx = Array.IndexOf(Modes, currentMode);
                                    currentMode = Modes[(idx + 1) % Modes.Length];
                                    actionCtx.Invalidate();
                                }, "Cycle mode");
                            }),
                            v.Separator(),
                            v.Text($"  Mode: {ModeLabel(currentMode)}  (Shift+Tab to change)"),
                        ]),
                        @yield: ctx => submittedText != null && submittedText != "/exit"
                            ? ctx.VStack(v =>
                            [
                                v.Text($"  > {submittedText}"),
                                v.Text($"  Echo: {submittedText}"),
                            ])
                            : ctx.Text(""),
                        options: new Hex1bFlowSliceOptions { MaxHeight = 5 }
                    );

                    if (submittedText == "/exit" || submittedText == null)
                        break;
                }
            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();
    }
}
