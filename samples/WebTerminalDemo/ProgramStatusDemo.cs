using Hex1b;
using Hex1b.Widgets;

namespace WebTerminalDemo;

internal static class ProgramStatusDemo
{
    internal static async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workload, CancellationToken ct)
    {
        var step = 0;
        var description = "No reports yet. Activate Next status to start.";
        await using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        [
            v.Text("OSC 7501: real Hex1bApp callback -> terminal -> HMP1/HWT1"),
            v.Text("Open Status in this view's title bar to inspect the inventory."),
            v.Text("Attach a second view with HMP1 relay to compare snapshots."),
            v.Text(""),
            v.Text($"Step {step}: {description}"),
            v.Button("Next status").OnClick(e =>
            {
                var status = e.Context.ProgramStatus;
                step = step % 9 + 1;
                switch (step)
                {
                    case 1:
                        status.Clear();
                        status.Report(new(TerminalProgramStatusState.Working)
                        {
                            App = "hex1b-demo", Message = "Deploying two regions"
                        });
                        status.Report(new(TerminalProgramStatusState.Working)
                        {
                            Id = "deploy/east", Title = "East", Progress = 40, Message = "Uploading"
                        });
                        status.Report(new(TerminalProgramStatusState.Blocked)
                        {
                            Id = "deploy/west", Title = "West", Kind = TerminalProgramStatusKind.Permission,
                            Message = "Approve production deployment?"
                        });
                        description = "Root working; East uploading; West needs permission. Missing deploy parent is only a group.";
                        break;
                    case 2:
                        status.Report(new(TerminalProgramStatusState.Working)
                        {
                            Id = "deploy/west", Title = "West", Progress = 60, Message = "Approved; deploying"
                        });
                        status.Report(new(TerminalProgramStatusState.Blocked)
                        {
                            Id = "deploy/east/auth", Kind = TerminalProgramStatusKind.Auth,
                            Message = "Login required"
                        });
                        description = "West resumed; East's nested auth record is blocked.";
                        break;
                    case 3:
                        status.Report(new(TerminalProgramStatusState.Blocked)
                        {
                            Id = "deploy/east/auth", Kind = TerminalProgramStatusKind.Question,
                            Message = "Which account should be used?"
                        });
                        description = "A blocked record changes from authentication to question.";
                        break;
                    case 4:
                        status.Report(new(TerminalProgramStatusState.Done)
                        {
                            Id = "deploy/east", Message = "East healthy"
                        });
                        status.Report(new(TerminalProgramStatusState.Error)
                        {
                            Id = "deploy/west", Message = "West health check failed"
                        });
                        description = "East done; West error. Replacement drops titles and progress; auth child still exists.";
                        break;
                    case 5:
                        status.Clear("deploy/east");
                        description = "Cleared East and its auth descendant; West remains.";
                        break;
                    case 6:
                        status.Report(new(TerminalProgramStatusState.Done)
                        {
                            App = "hex1b-demo", Message = "<b>Finished</b> is plain text, not markup"
                        });
                        description = "Root done; West's error remains independently visible.";
                        break;
                    case 7:
                        status.Report(new(TerminalProgramStatusState.Idle)
                        {
                            App = "hex1b-demo", Message = "Ready for another instruction"
                        });
                        description = "Root idle is different from unread completion.";
                        break;
                    case 8:
                        status.Clear("deploy");
                        description = "Cleared the missing parent deploy and all descendants; root remains.";
                        break;
                    default:
                        status.Clear();
                        description = "Cleared the entire inventory. Next status restarts.";
                        break;
                }
            }),
            v.Button("Quit").OnClick(e => e.Context.RequestStop())
        ]), new Hex1bAppOptions { WorkloadAdapter = workload });
        await app.RunAsync(ct);
    }
}
