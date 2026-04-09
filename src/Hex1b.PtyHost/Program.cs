using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hex1b.PtyHost;

internal static class Program
{
    internal static IHost BuildApplication(string[] args)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var logFile = TryGetOptionValue(args, "--logfile");
        if (!string.IsNullOrWhiteSpace(logFile))
        {
            builder.Logging.AddProvider(new PtyHostFileLoggerProvider(logFile));
        }

        builder.Services.AddSingleton<PtyHostRunner>();
        builder.Services.AddTransient<RootCommand>(sp => CreateRootCommand(sp));

        return builder.Build();
    }

    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        using var app = BuildApplication(args);
        await app.StartAsync(cts.Token).ConfigureAwait(false);

        var rootCommand = app.Services.GetRequiredService<RootCommand>();
        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync(cancellationToken: cts.Token).ConfigureAwait(false);
    }

    private static RootCommand CreateRootCommand(IServiceProvider services)
    {
        var socketOption = new Option<string>("--socket")
        {
            Description = "Unix-domain socket path for the Hex1b PTY session.",
            Required = true
        };

        var tokenOption = new Option<string>("--token")
        {
            Description = "Per-launch token used to authenticate the owning Hex1b process.",
            Required = true
        };

        var logFileOption = new Option<string?>("--logfile")
        {
            Description = "Optional file path for helper logs."
        };

        var rootCommand = new RootCommand("Internal Hex1b Windows PTY host.");
        rootCommand.Options.Add(socketOption);
        rootCommand.Options.Add(tokenOption);
        rootCommand.Options.Add(logFileOption);

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<PtyHostRunner>();
            var options = new PtyHostOptions(
                parseResult.GetValue(socketOption)!,
                parseResult.GetValue(tokenOption)!,
                parseResult.GetValue(logFileOption));

            return await runner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        });

        return rootCommand;
    }

    private static string? TryGetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
