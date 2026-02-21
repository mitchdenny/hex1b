using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the "flowdemo copilot" command — a mock Copilot CLI chat interface.
/// Each prompt/response pair is yielded as frozen output that scrolls up naturally.
/// Type /exit to end the session.
/// </summary>
internal static class CopilotCommand
{
    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;

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
                            v.Separator(),
                            v.TextBox().OnSubmit(e =>
                            {
                                submittedText = e.Text;
                                e.Context.RequestStop();
                            }),
                            v.Separator(),
                        ]),
                        @yield: ctx => submittedText != null && submittedText != "/exit"
                            ? ctx.VStack(v =>
                            [
                                v.Text($"  > {submittedText}"),
                                v.Text($"  Echo: {submittedText}"),
                            ])
                            : ctx.Text(""),
                        options: new Hex1bFlowSliceOptions { MaxHeight = 3 }
                    );

                    if (submittedText == "/exit" || submittedText == null)
                        break;
                }
            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();
    }
}
