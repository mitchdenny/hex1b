#:sdk Aspire.AppHost.Sdk@13.2.0-preview.1.25611.1
#:package Aspire.Hosting.JavaScript@13.2.0-preview.1.25611.1
#:package Aspire.Hosting.Docker@13.2.0-preview.1.25611.1

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

// Build Hex1b library first to generate XML documentation
var hexlib = builder.AddExecutable("hexlib-build", "dotnet", "./src/Hex1b", "build", "--no-restore");

// Generate API docs from XML before starting content
var docsGenerator = builder.AddExecutable("docs-generator", "node", "./src/content", "scripts/generate-api-docs.mjs")
    .WaitFor(hexlib);

var website = builder.AddCSharpApp("website", "./src/Hex1b.Website")
    .WithExternalHttpEndpoints();

var content = builder.AddViteApp("content", "./src/content")
    .WithReference(website)
    .WaitFor(website)
    .WaitFor(docsGenerator);

website.PublishWithContainerFiles(content, "./wwwroot");

builder.Build().Run();