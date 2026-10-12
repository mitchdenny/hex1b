using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREPIPELINES001

namespace Aspire.Hosting;

/// <summary>Provides repository-local GitHub Pages hosting integration.</summary>
public static class GitHubPagesHostingExtensions
{
    /// <summary>
    /// Adds a Vite development server and resource-specific GitHub Pages pipeline steps.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The Pages resource name.</param>
    /// <param name="appDirectory">The Vite directory relative to the AppHost.</param>
    /// <param name="repository">The GitHub repository in owner/name form.</param>
    /// <returns>The Pages resource builder.</returns>
    /// <remarks>
    /// The build-github-pages-NAME step builds locally without credentials.
    /// The publish-github-pages-NAME step builds, uploads, and deploys from a
    /// GitHub Actions JavaScript action context. Neither joins the default
    /// build, publish, or deploy pipeline.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// builder.AddGitHubPages("new-site", "./src/site", "mitchdenny/hex1b");
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<GitHubPagesResource> AddGitHubPages(
        this IDistributedApplicationBuilder builder,
        string name,
        string appDirectory,
        string repository)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        if (!Regex.IsMatch(repository, @"^[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+$") ||
            repository.Split('/')[1] is "." or "..")
        {
            throw new ArgumentException("The repository must use owner/name form.", nameof(repository));
        }

        var resource = new GitHubPagesResource(
            name, Path.GetFullPath(appDirectory, builder.AppHostDirectory), repository);
        var helperDirectory = Path.Combine(builder.AppHostDirectory, ".github", "scripts", "github-pages");
        var resourceBuilder = builder.AddResource(resource);

        if (builder.ExecutionContext.IsRunMode)
        {
            builder.AddViteApp($"{name}-dev", resource.SourceDirectory)
                .WithNpm(installCommand: "ci");
        }

        return resourceBuilder.WithPipelineStepFactory(_ => new[]
        {
            new PipelineStep
            {
                Name = $"build-github-pages-{name}",
                Description = "Build the Vite static bundle without publishing.",
                Action = async context =>
                {
                    await PagesProcess.RunAsync("npm", ["ci", "--no-audit", "--no-fund"],
                        resource.SourceDirectory, context.Logger, context.CancellationToken);
                    await PagesProcess.RunAsync("npm", ["run", "build"],
                        resource.SourceDirectory, context.Logger, context.CancellationToken);
                    if (!File.Exists(Path.Combine(resource.BundleDirectory, "index.html")))
                    {
                        throw new InvalidOperationException("The Vite build did not produce dist/index.html.");
                    }
                    await PagesProcess.RunAsync("node",
                        ["check-bundle.mjs", resource.BundleDirectory, $"/{repository.Split('/')[1]}/"],
                        helperDirectory, context.Logger, context.CancellationToken);
                }
            },
            new PipelineStep
            {
                Name = $"publish-github-pages-{name}",
                Description = "Upload and deploy the static bundle to GitHub Pages.",
                DependsOnSteps = [$"build-github-pages-{name}"],
                Action = async context =>
                {
                    await PagesProcess.RunAsync("npm", ["ci", "--no-audit", "--no-fund"],
                        helperDirectory, context.Logger, context.CancellationToken);
                    await PagesProcess.RunAsync("node",
                        ["publish.mjs", resource.BundleDirectory, repository, $"github-pages-{name}"],
                        helperDirectory, context.Logger, context.CancellationToken);
                    context.Logger.LogInformation("GitHub Pages publication completed for {Repository}.", repository);
                    context.PipelineContext.Summary.Add("GitHub Pages", resource.PublishedUrl.AbsoluteUri);
                }
            }
        });
    }
}
