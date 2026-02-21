using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Hex1b.Benchmarks;

// Parse command line args to select benchmark
var benchmarkType = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
var bdnArgs = args.Length > 1 ? args[1..] : [];

switch (benchmarkType)
{
    case "surface":
        BenchmarkSwitcher.FromTypes([typeof(SurfaceBenchmarks)]).Run(bdnArgs);
        break;
    case "rendering":
        BenchmarkSwitcher.FromTypes([typeof(RenderingModeBenchmarks)]).Run(bdnArgs);
        break;
    case "all":
    default:
        BenchmarkSwitcher.FromTypes([typeof(SurfaceBenchmarks), typeof(RenderingModeBenchmarks)]).Run(bdnArgs);
        break;
}
