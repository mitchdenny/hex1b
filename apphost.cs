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

// Generate API reference documentation
var docGenerator = builder.AddCSharpApp("docfx", "./src/DocGenerator")
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
    .WithReference(website)
    .WaitFor(website)
    .WaitFor(docGenerator)
    .WithEndpoint("http", ep => ep.Port = 1189);

website.PublishWithContainerFiles(content, "./wwwroot");

builder.Build().Run();