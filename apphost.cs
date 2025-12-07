#:sdk Aspire.AppHost.Sdk@13.1.0-preview.1.25605.5

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddCSharpApp("websockets", "./samples/websockets");

builder.Build().Run();