using Hex1b;
using LogicBuilderDemo.Models;

var state = new AppState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        state.App = app;
        return ctx => LogicBuilderApp.Build(ctx, state);
    })
    .WithMouse()
    .WithDiagnostics()
    .Build();

await terminal.RunAsync();
