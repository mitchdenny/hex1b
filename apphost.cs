#:sdk Aspire.AppHost.Sdk@13.2.0-preview.1.26106.8
#:package Aspire.Hosting.JavaScript@13.2.0-preview.1.26106.8
#:package Aspire.Hosting.Azure.AppContainers@13.2.0-preview.1.26106.8

using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

#pragma warning disable ASPIRECSHARPAPPS001
#pragma warning disable ASPIREACADOMAINS001
#pragma warning disable ASPIREPIPELINES001

var builder = DistributedApplication.CreateBuilder(args);

// Conditionally adds parameters to app model if config values are specified.
var customDomainValue = builder.Configuration["Parameters:customDomain"];
var certificateNameValue = builder.Configuration["Parameters:certificateName"];
var minReplicasValue = builder.Configuration["Parameters:minReplicas"];

var customDomain = !string.IsNullOrEmpty(customDomainValue) ? builder.AddParameter("customDomain") : null;
var certificateName = !string.IsNullOrEmpty(certificateNameValue) ? builder.AddParameter("certificateName") : null;

// Parse minReplicas from config, defaulting to 0 for PR deployments
var minReplicas = int.TryParse(minReplicasValue, out var parsedMinReplicas) ? parsedMinReplicas : 0;

builder.AddAzureContainerAppEnvironment("env");

// Generate API reference documentation - excluded from manifest but used during build
var docGenerator = builder.AddCSharpApp("docfx", "./src/DocGenerator")
    .ExcludeFromManifest()
    .WithPipelineStepFactory(context => new PipelineStep
    {
        Name = "generate-docs",
        Description = "Generates API reference documentation from Hex1b library",
        Action = async ctx =>
        {
            var docGenPath = Path.Combine(builder.AppHostDirectory, "src", "DocGenerator");
            var psi = new ProcessStartInfo("dotnet", "run")
            {
                WorkingDirectory = docGenPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            ctx.Logger.LogInformation("Running DocGenerator to generate API reference docs...");
            
            using var process = Process.Start(psi);
            if (process is null)
            {
                throw new InvalidOperationException("Failed to start DocGenerator process");
            }
            
            await process.WaitForExitAsync(ctx.CancellationToken);
            
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"DocGenerator failed with exit code {process.ExitCode}: {error}");
            }
            
            ctx.Logger.LogInformation("API reference documentation generated successfully");
        },
        Tags = ["generate-docs"],
        RequiredBySteps = [WellKnownPipelineSteps.Build],
        DependsOnSteps = [WellKnownPipelineSteps.BuildPrereq]
    });

var website = builder.AddCSharpApp("website", "./src/Hex1b.Website")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = minReplicas;
        app.Template.Scale.MaxReplicas = 1;

        if (customDomain is not null && certificateName is not null)
        {
            app.ConfigureCustomDomain(customDomain, certificateName);
        }
    });

// Tree widget demo
var treeDemo = builder.AddCSharpApp("tree-demo", "./samples/TreeDemo")
    .ExcludeFromManifest();

// Window widget demo
var windowingDemo = builder.AddCSharpApp("windowing-demo", "./samples/WindowingDemo")
    .ExcludeFromManifest();

// Logger panel demo
var loggerPanelDemo = builder.AddCSharpApp("logger-panel-demo", "./samples/LoggerPanelDemo")
    .ExcludeFromManifest();

// DragBarPanel widget demo
var dragBarPanelDemo = builder.AddCSharpApp("dragbar-panel-demo", "./samples/DragBarPanelDemo")
    .ExcludeFromManifest();

// WidgetLayer demo
var widgetLayerDemo = builder.AddCSharpApp("widget-layer-demo", "./samples/WidgetLayerDemo")
    .ExcludeFromManifest();

// Charting demo
var chartingDemo = builder.AddCSharpApp("charting-demo", "./samples/ChartingDemo")
    .ExcludeFromManifest();

// Interactable demo
var interactableDemo = builder.AddCSharpApp("interactable-demo", "./samples/InteractableDemo")
    .ExcludeFromManifest();

var content = builder.AddViteApp("content", "./src/content")
    .WithReference(website)
    .WaitFor(website)
    .WaitFor(docGenerator)
    .WithEndpoint("http", ep => ep.Port = 1189);

// Wire up pipeline dependencies: content build depends on doc generation
content.WithPipelineConfiguration(context =>
{
    // Make content build steps depend on doc generator step
    var buildSteps = context.GetSteps(content.Resource, WellKnownPipelineTags.BuildCompute);
    var docGenSteps = context.GetSteps(docGenerator.Resource, "generate-docs");
    buildSteps.DependsOn(docGenSteps);
});

website.PublishWithContainerFiles(content, "./wwwroot");

builder.Build().Run();