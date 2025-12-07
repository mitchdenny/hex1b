#:package Aspire.Hosting.DevTunnels@13.1.0-preview.1.25605.5
#:sdk Aspire.AppHost.Sdk@13.1.0-preview.1.25605.5

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var gallery = builder.AddCSharpApp("gallery", "./samples/Gallery");

var tunnel = builder.AddDevTunnel("tunnel")
    .WithReference(gallery);

builder.Build().Run();