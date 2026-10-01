using Hex1b;

var state = new TaskState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Task: {state.TaskName}"),
            v.Text($"Priority: {state.Priority}"),
            v.Text(""),
            v.HStack(h => [
                h.SplitButton()
                   .PrimaryAction("Create Task", _ => state.TaskName = "New Task")
                   .SecondaryAction("From Template", _ => state.TaskName = "Template Task")
                   .SecondaryAction("Duplicate Last", _ => state.TaskName = "Duplicated Task"),
                h.Text(" "),
                h.SplitButton()
                   .PrimaryAction("Set Priority", _ => state.Priority = "Normal")
                   .SecondaryAction("Low", _ => state.Priority = "Low")
                   .SecondaryAction("High", _ => state.Priority = "High")
                   .SecondaryAction("Urgent", _ => state.Priority = "Urgent")
            ])
        ])
    ]).Title("Task Manager"))
    .Build();

await terminal.RunAsync();

class TaskState
{
    public string TaskName { get; set; } = "(none)";
    public string Priority { get; set; } = "Normal";
}
