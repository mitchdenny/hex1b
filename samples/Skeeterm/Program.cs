using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHex1bApp();

var host = builder.Build();
var app = host.Services.GetRequiredService<Hex1bApp>();

var pendingHostRun = host.RunAsync();
var pendingAppRun = app.RunAsync();

await Task.WhenAny(pendingHostRun, pendingAppRun);

public class AppState
{
}

public static class AppExtensions
{
    public static IServiceCollection AddHex1bApp(this IServiceCollection services)
    {
        services.AddSingleton<AppState>();
        services.AddSingleton<Hex1bApp>(sp =>
        {
            var state = sp.GetRequiredService<AppState>();
            var app = BuildApp(state);
            return app;
        });

        return services;
    }

    private static Hex1bApp BuildApp(AppState state)
    {
        return new Hex1bApp(
            context => {
                var vstack = context.VStack(v => [
                    v.ContentPanel().Fill(),
                    v.InfoBar("Skeetterm"),
                    v.Button("Exit", e => e.Context.RequestStop())
                ]);
                return vstack;
            }
        );
    }

    private static Hex1bWidget ContentPanel(this WidgetContext<VStackWidget> context)
    {
        return context.Panel(context => [context.Text("Hello world!")]);
    }
}