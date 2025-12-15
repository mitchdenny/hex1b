#:package Aspire.Hosting.DevTunnels@13.2.0-preview.1.25611.1
#:sdk Aspire.AppHost.Sdk@13.2.0-preview.1.25611.1

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var website = builder.AddCSharpApp("website", "./src/Hex1b.Website");

var tunnel = builder.AddDevTunnel("tunnel")
    .WithReference(website);

builder.Build().Run();