using CloudTermDemo;
using Hex1b;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<SplashScreen>();
builder.Services.AddSingleton<FirstRunExperience>();
builder.Services.AddSingleton<MainScreen>();
builder.Services.AddSingleton<TutorialService>();
builder.Services.AddSingleton(_ => new CloudShellState(CloudModel.BuildHierarchy()));
builder.Services.AddSingleton<PanelManager>();

// Node command providers — one per resource type
builder.Services.AddSingleton<INodeCommandProvider, PodCommandProvider>();
builder.Services.AddSingleton<INodeCommandProvider, NamespaceCommandProvider>();
builder.Services.AddSingleton<INodeCommandProvider, AksClusterCommandProvider>();
builder.Services.AddSingleton<INodeCommandProvider, AppServiceCommandProvider>();
builder.Services.AddSingleton<INodeCommandProvider, SqlServerCommandProvider>();
builder.Services.AddSingleton<NodeCommandRegistry>();

builder.Services.AddSingleton<CloudTerminalHost>();
builder.Services.AddSingleton<ShellScreen>();

var host = builder.Build();

var appState = host.Services.GetRequiredService<AppState>();
var splashScreen = host.Services.GetRequiredService<SplashScreen>();
var firstRunExperience = host.Services.GetRequiredService<FirstRunExperience>();
var shellScreen = host.Services.GetRequiredService<ShellScreen>();
var mainScreen = host.Services.GetRequiredService<MainScreen>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        appState.CurrentScreen switch
        {
            AppScreen.Splash => splashScreen.Build(ctx, app),
            AppScreen.FirstRun => firstRunExperience.Build(ctx, app),
            AppScreen.Shell => shellScreen.Build(ctx, app),
            AppScreen.Main => mainScreen.Build(ctx, app),
            _ => mainScreen.Build(ctx, app),
        })
    .Build();

await terminal.RunAsync();
