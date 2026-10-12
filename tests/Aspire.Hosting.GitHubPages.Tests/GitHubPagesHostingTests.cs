using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspire.Hosting.GitHubPages.Tests;

[TestClass]
public class GitHubPagesHostingTests
{
    [TestMethod]
    public async Task AddGitHubPages_PublishMode_RegistersOnlyIsolatedNamedSteps()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"],
            DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        var pages = builder.AddGitHubPages("new-site", "./site", "mitchdenny/hex1b");
        Assert.AreEqual(Path.GetFullPath("site", builder.AppHostDirectory), pages.Resource.SourceDirectory);
        Assert.AreEqual(Path.Combine(pages.Resource.SourceDirectory, "dist"), pages.Resource.BundleDirectory);
        Assert.AreEqual("mitchdenny/hex1b", pages.Resource.Repository);
        Assert.AreEqual("https://mitchdenny.github.io/hex1b/", pages.Resource.PublishedUrl.AbsoluteUri);
        Assert.AreEqual(1, builder.Resources.Count);

        using var services = builder.Services.BuildServiceProvider();
        var annotation = TestSeq.Single(pages.Resource.Annotations.OfType<PipelineStepAnnotation>());
        var steps = (await annotation.CreateStepsAsync(new PipelineStepFactoryContext
        {
            Resource = pages.Resource,
            PipelineContext = new PipelineContext(
                new DistributedApplicationModel(builder.Resources),
                builder.ExecutionContext, services, NullLogger.Instance, CancellationToken.None)
        })).ToArray();
        TestSeq.AreEqual(new[] { "build-github-pages-new-site", "publish-github-pages-new-site" },
            steps.Select(step => step.Name));
        Assert.IsEmpty(steps[0].DependsOnSteps);
        TestSeq.AreEqual(new[] { "build-github-pages-new-site" }, steps[1].DependsOnSteps);
        Assert.IsTrue(steps.All(step => step.RequiredBySteps.Count == 0 && step.Tags.Count == 0));
    }

    [TestMethod]
    public void AddGitHubPages_RunMode_AddsViteDevelopmentServer()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        builder.Configuration["AppHost:Sha256"] = new string('a', 64);
        builder.AddGitHubPages("new-site", "./site", "mitchdenny/hex1b");
        var vite = TestSeq.Single(builder.Resources.OfType<ViteAppResource>());
        Assert.AreEqual("new-site-dev", vite.Name);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("owner/repo/other")]
    [DataRow("owner/..")]
    [DataRow("owner/repo?")]
    public void AddGitHubPages_InvalidRepository_Throws(string repository)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"], DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        Assert.ThrowsExactly<ArgumentException>(() => builder.AddGitHubPages("site", "./site", repository));
    }

    [TestMethod]
    public async Task RunAsync_NonzeroExit_ThrowsWithExitCode()
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            PagesProcess.RunAsync("node", ["-e", "process.exit(7)"], AppContext.BaseDirectory,
                NullLogger.Instance, TestContext.Current.CancellationToken));
        StringAssert.Contains(exception.Message, "exit code 7");
    }

    [TestMethod]
    public async Task RunAsync_CanceledBeforeStart_DoesNotLaunchProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PagesProcess.RunAsync("not-an-executable", [], AppContext.BaseDirectory,
                NullLogger.Instance, cancellation.Token));
    }

    [TestMethod]
    public async Task RunAsync_ArgumentsWithSpaces_PreservesArgumentBoundaries()
    {
        await PagesProcess.RunAsync("node",
            ["-e", "if(process.argv[1] !== 'argument with spaces') process.exit(1)", "argument with spaces"],
            AppContext.BaseDirectory, NullLogger.Instance, TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_LargeOutputOnBothStreams_DrainsBeforeReturning()
    {
        var logger = new ProcessLogger();
        await PagesProcess.RunAsync("node",
            ["-e", "process.stdout.write('o'.repeat(65536)+'\\n');process.stderr.write('e'.repeat(65536)+'\\n')"],
            AppContext.BaseDirectory, logger, TestContext.Current.CancellationToken);
        Assert.IsTrue(logger.Messages.Any(message => message == new string('o', 65536)));
        Assert.IsTrue(logger.Messages.Any(message => message == new string('e', 65536)));
    }

    [TestMethod]
    public async Task RunAsync_CanceledWhileRunning_TerminatesChild()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var logger = new ProcessLogger();
        var task = PagesProcess.RunAsync("node",
            ["-e", "console.log('ready:'+process.pid);setInterval(()=>{},1000)"],
            AppContext.BaseDirectory, logger, cancellation.Token);
        try
        {
            var pid = await logger.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                Assert.IsTrue(process.HasExited);
            }
            catch (ArgumentException)
            {
                // The exited child has already been reaped.
            }
        }
        finally
        {
            cancellation.Cancel();
            try { await task; }
            catch (OperationCanceledException) { }
        }
    }

    private sealed class ProcessLogger : ILogger
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        internal TaskCompletionSource<int> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Messages.Enqueue(message);
            if (message.StartsWith("ready:", StringComparison.Ordinal))
            {
                Ready.TrySetResult(int.Parse(message["ready:".Length..]));
            }
        }
    }
}
