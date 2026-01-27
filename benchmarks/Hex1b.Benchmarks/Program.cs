using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Hex1b.Benchmarks;

// Parse command line args to select benchmark
var benchmarkType = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

switch (benchmarkType)
{
    case "surface":
        BenchmarkRunner.Run<SurfaceBenchmarks>();
        break;
    case "rendering":
        BenchmarkRunner.Run<RenderingModeBenchmarks>();
        break;
    case "all":
    default:
        BenchmarkRunner.Run<SurfaceBenchmarks>();
        BenchmarkRunner.Run<RenderingModeBenchmarks>();
        break;
}
