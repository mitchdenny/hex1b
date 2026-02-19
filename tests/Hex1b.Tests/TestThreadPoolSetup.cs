using System.Runtime.CompilerServices;

namespace Hex1b.Tests;

/// <summary>
/// Ensures the .NET thread pool has enough threads for parallel test execution.
/// Each Hex1bTerminal spawns 2 Task.Run pump threads (input + output). Without
/// sufficient min threads, parallel tests cause thread pool starvation on CI
/// runners with few cores (e.g., 2-core GitHub Actions runners).
/// </summary>
internal static class TestThreadPoolSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // CI runners typically have 2 cores → ThreadPool starts with 2 min threads.
        // With 4 parallel tests × 2 pump threads = 8 threads needed immediately.
        // WaitUntil polling adds more continuations. Set a generous minimum to
        // avoid slow thread pool ramp-up (which adds ~1 thread/second).
        ThreadPool.SetMinThreads(32, 32);
    }
}
