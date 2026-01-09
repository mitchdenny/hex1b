#:sdk Aspire.AppHost.Sdk@13.1.0
#:package Aspire.Hosting.JavaScript@13.1.0
#:package Aspire.Hosting.Azure.AppContainers@13.1.0

#pragma warning disable ASPIRECSHARPAPPS001
#pragma warning disable ASPIREACADOMAINS001

var builder = DistributedApplication.CreateBuilder(args);

// Conditionally adds parameters to app model if config values are specified.
var customDomainValue = builder.Configuration["Parameters:customDomain"];
var certificateNameValue = builder.Configuration["Parameters:certificateName"];

var customDomain = !string.IsNullOrEmpty(customDomainValue) ? builder.AddParameter("customDomain") : null;
var certificateName = !string.IsNullOrEmpty(certificateNameValue) ? builder.AddParameter("certificateName") : null;

builder.AddAzureContainerAppEnvironment("env");

// DocFX API documentation server
IResourceBuilder<ExecutableResource>? docfx = null;
docfx = builder.AddExecutable("docfx", "dotnet", "./src/docfx", "docfx", "docfx.json", "--serve")
    .WithHttpEndpoint(name: "http", targetPort: null)
    .WithArgs(context =>
    {
        var endpoint = docfx!.GetEndpoint("http");
        context.Args.Add("-p");
        context.Args.Add(endpoint.Property(EndpointProperty.TargetPort));
    })
    .ExcludeFromManifest();

var website = builder.AddCSharpApp("website", "./src/Hex1b.Website")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 0;
        app.Template.Scale.MaxReplicas = 1;

        if (customDomain is not null && certificateName is not null)
        {
            app.ConfigureCustomDomain(customDomain, certificateName);
        }
    });

var content = builder.AddViteApp("content", "./src/content")
    .WithViteConfig("./src/content/docs/.vitepress/config.ts")
    .WithReference(website)
    .WaitFor(website)
    .WithEndpoint("http", ep => ep.Port = 1189);

website.PublishWithContainerFiles(content, "./wwwroot");

builder.Build().Run();