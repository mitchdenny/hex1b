using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Split Button Documentation: Multiple Buttons
/// Demonstrates multiple split buttons in a toolbar.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the multipleCode sample in:
/// src/content/guide/widgets/split-button.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SplitButtonMultipleExample(ILogger<SplitButtonMultipleExample> logger) : Hex1bExample
{
    private readonly ILogger<SplitButtonMultipleExample> _logger = logger;

    public override string Id => "split-button-multiple";
    public override string Title => "Split Button - Multiple Buttons";
    public override string Description => "Demonstrates multiple split buttons in a toolbar";

    private class TaskState
    {
        public string TaskName { get; set; } = "(none)";
        public string Priority { get; set; } = "Normal";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating split button multiple example widget builder");

        var state = new TaskState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text($"Task: {state.TaskName}"),
                    v.Text($"Priority: {state.Priority}"),
                    v.Text(""),
                    v.HStack(h => [
                        h.SplitButton("Create Task")
                           .OnPrimaryClick(_ => state.TaskName = "New Task")
                           .WithSecondaryAction("From Template", _ => state.TaskName = "Template Task")
                           .WithSecondaryAction("Duplicate Last", _ => state.TaskName = "Duplicated Task"),
                        h.Text(" "),
                        h.SplitButton("Set Priority")
                           .OnPrimaryClick(_ => state.Priority = "Normal")
                           .WithSecondaryAction("Low", _ => state.Priority = "Low")
                           .WithSecondaryAction("High", _ => state.Priority = "High")
                           .WithSecondaryAction("Urgent", _ => state.Priority = "Urgent")
                    ])
                ])
            ], title: "Task Manager");
        };
    }
}
