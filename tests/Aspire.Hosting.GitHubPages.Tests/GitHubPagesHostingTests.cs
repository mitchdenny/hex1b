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
    public async Task AddGitHubPages_PublishMode_DependsOnNativeContainerBuild()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"],
            DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        var site = builder.AddViteApp("site", "./site").WithNpm(installCommand: "ci");
        var pages = builder.AddGitHubPages("new-site", site, "mitchdenny/hex1b");
        Assert.AreSame(site.Resource, pages.Resource.Source);
        Assert.AreEqual(Path.Combine(builder.AppHostDirectory, ".aspire", "github-pages", "new-site", "dist"),
            pages.Resource.BundleDirectory);
        Assert.AreEqual("mitchdenny/hex1b", pages.Resource.Repository);
        Assert.AreEqual("https://mitchdenny.github.io/hex1b/", pages.Resource.PublishedUrl.AbsoluteUri);
        Assert.AreEqual(2, builder.Resources.Count);
        var destination = TestSeq.Single(pages.Resource.Annotations.OfType<ContainerFilesDestinationAnnotation>());
        Assert.AreSame(site.Resource, destination.Source);
        Assert.AreEqual(pages.Resource.BundleDirectory, destination.DestinationPath);

        using var services = builder.Services.BuildServiceProvider();
        var model = new DistributedApplicationModel(builder.Resources);
        var pipeline = new PipelineContext(model, builder.ExecutionContext, services,
            NullLogger.Instance, CancellationToken.None);
        var annotation = TestSeq.Single(pages.Resource.Annotations.OfType<PipelineStepAnnotation>());
        var steps = (await annotation.CreateStepsAsync(new PipelineStepFactoryContext
        {
            Resource = pages.Resource,
            PipelineContext = pipeline
        })).ToArray();
        TestSeq.AreEqual(new[] { "extract-new-site", "deploy-new-site" },
            steps.Select(step => step.Name));
        Assert.IsEmpty(steps[0].DependsOnSteps);
        TestSeq.AreEqual(new[] { "extract-new-site" }, steps[1].DependsOnSteps);
        Assert.IsTrue(steps.All(step => step.RequiredBySteps.Count == 0 && step.Tags.Count == 0));

        var source = TestSeq.Single(model.Resources.OfType<ContainerResource>());
        var nativeSteps = new List<PipelineStep>();
        foreach (var factory in source.Annotations.OfType<PipelineStepAnnotation>())
        {
            nativeSteps.AddRange(await factory.CreateStepsAsync(new PipelineStepFactoryContext
            {
                Resource = source, PipelineContext = pipeline
            }));
        }
        var nativeBuild = TestSeq.Single(nativeSteps.Where(step => step.Tags.Contains(WellKnownPipelineTags.BuildCompute)));
        Assert.AreEqual("build-site", nativeBuild.Name);
        var configuration = TestSeq.Single(pages.Resource.Annotations.OfType<PipelineConfigurationAnnotation>());
        await configuration.Callback(new PipelineConfigurationContext
        {
            Services = services, Model = model, Steps = [.. nativeSteps, .. steps]
        });
        TestSeq.AreEqual(new[] { nativeBuild.Name }, steps[0].DependsOnSteps);
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
        Assert.AreEqual("new-site-vite", vite.Name);
        var pages = TestSeq.Single(builder.Resources.OfType<GitHubPagesResource>());
        Assert.AreSame(vite, pages.Source);
        Assert.AreEqual(1, builder.Resources.OfType<ViteAppResource>().Count());
    }

    [TestMethod]
    public void AddGitHubPages_SourceFromAnotherAppHost_Throws()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"], DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        var other = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"], DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        var site = other.AddViteApp("site", "./site");
        Assert.ThrowsExactly<ArgumentException>(() => builder.AddGitHubPages("pages", site, "mitchdenny/hex1b"));
        Assert.IsEmpty(builder.Resources);
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("../escape")]
    [DataRow("nested/site")]
    [DataRow("nested\\site")]
    public void AddGitHubPages_NameEscapesOutputDirectory_Throws(string name)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish"], DisableDashboard = true,
            ProjectDirectory = AppContext.BaseDirectory
        });
        var site = builder.AddViteApp("site", "./site");
        Assert.ThrowsExactly<ArgumentException>(() => builder.AddGitHubPages(name, site, "mitchdenny/hex1b"));
        Assert.AreEqual(1, builder.Resources.Count);
    }

    [TestMethod]
    public async Task ExtractAsync_Success_CopiesWithoutStartingAndRemovesContainer()
    {
        var commands = new List<string[]>();
        await PagesContainerFiles.ExtractAsync((arguments, _) =>
        {
            commands.Add(arguments);
            return Task.CompletedTask;
        }, "site:built-tag", "/app/dist", "directory with spaces", TestContext.Current.CancellationToken);
        var container = commands[0][4];
        Assert.IsTrue(container.StartsWith("aspire-pages-", StringComparison.Ordinal));
        TestSeq.AreEqual(new[] { "create", "--pull", "never", "--name", container, "--entrypoint", "/bin/true", "site:built-tag" }, commands[0]);
        TestSeq.AreEqual(new[] { "cp", $"{container}:/app/dist/.", "directory with spaces" }, commands[1]);
        TestSeq.AreEqual(new[] { "rm", "--force", "--volumes", container }, commands[2]);
        Assert.AreEqual(3, commands.Count);
    }

    [TestMethod]
    [DataRow("create")]
    [DataRow("cp")]
    public async Task ExtractAsync_FailedCommand_AttemptsCleanupAndPreservesError(string failedCommand)
    {
        var commands = new List<string[]>();
        var error = new InvalidOperationException("extraction failed");
        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            PagesContainerFiles.ExtractAsync((arguments, _) =>
            {
                commands.Add(arguments);
                return arguments[0] == failedCommand ? Task.FromException(error) : Task.CompletedTask;
            }, "site:latest", "/app/dist/", "destination", TestContext.Current.CancellationToken));
        Assert.AreSame(error, actual);
        Assert.AreEqual("rm", commands[^1][0]);
    }

    [TestMethod]
    public async Task ExtractAsync_CanceledCopy_UsesIndependentCleanupToken()
    {
        using var cancellation = new CancellationTokenSource();
        var cleaned = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PagesContainerFiles.ExtractAsync((arguments, token) =>
            {
                if (arguments[0] == "cp")
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                if (arguments[0] == "rm")
                {
                    Assert.IsFalse(token.IsCancellationRequested);
                    Assert.IsTrue(token.CanBeCanceled);
                    cleaned = true;
                }
                return Task.CompletedTask;
            }, "site:latest", "/app/dist", "destination", cancellation.Token));
        Assert.IsTrue(cleaned);
    }

    [TestMethod]
    public async Task ExtractAsync_ExtractionAndCleanupFail_ReportsBothErrors()
    {
        var copy = new InvalidOperationException("copy failed");
        var cleanup = new InvalidOperationException("cleanup failed");
        var error = await Assert.ThrowsExactlyAsync<AggregateException>(() =>
            PagesContainerFiles.ExtractAsync((arguments, _) => arguments[0] switch
            {
                "cp" => Task.FromException(copy),
                "rm" => Task.FromException(cleanup),
                _ => Task.CompletedTask
            }, "site:latest", "/app/dist", "destination", TestContext.Current.CancellationToken));
        TestSeq.AreEqual(new Exception[] { copy, cleanup }, error.InnerExceptions);
    }

    [TestMethod]
    public async Task ExtractAsync_CleanupFails_DoesNotReportSuccess()
    {
        var cleanup = new InvalidOperationException("cleanup failed");
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            PagesContainerFiles.ExtractAsync((arguments, _) =>
                arguments[0] == "rm" ? Task.FromException(cleanup) : Task.CompletedTask,
                "site:latest", "/app/dist", "destination", TestContext.Current.CancellationToken));
        Assert.AreSame(cleanup, error);
    }

    [TestMethod]
    public async Task ExtractAsync_PreCanceled_DoesNotCreateContainer()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executed = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PagesContainerFiles.ExtractAsync((_, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            }, "site:latest", "/app/dist", "destination", cancellation.Token));
        Assert.IsFalse(executed);
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
        Assert.IsEmpty(builder.Resources);
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
