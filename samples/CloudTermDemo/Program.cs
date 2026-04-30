using CloudTermDemo;
using Hex1b;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<SplashScreen>();
builder.Services.AddSingleton<MainScreen>();

var host = builder.Build();

var appState = host.Services.GetRequiredService<AppState>();
var splashScreen = host.Services.GetRequiredService<SplashScreen>();
var mainScreen = host.Services.GetRequiredService<MainScreen>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        appState.CurrentScreen switch
        {
            AppScreen.Splash => splashScreen.Build(ctx, app),
            AppScreen.Main => mainScreen.Build(ctx, app),
            _ => mainScreen.Build(ctx, app),
        })
    .Build();

await terminal.RunAsync();
