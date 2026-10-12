using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIRECONTAINERRUNTIME001
#pragma warning disable ASPIREFILESYSTEM001

namespace Aspire.Hosting;

/// <summary>Provides repository-local GitHub Pages hosting integration.</summary>
public static class GitHubPagesHostingExtensions
{
    /// <summary>
    /// Adds a Vite app and publishes its built container files to GitHub Pages.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The Pages resource name.</param>
    /// <param name="appDirectory">The Vite directory relative to the AppHost.</param>
    /// <param name="repository">The GitHub repository in owner/name form.</param>
    /// <returns>The Pages resource builder.</returns>
    /// <remarks>
    /// The extract-NAME step builds a container image and extracts its static files.
    /// The deploy-NAME step builds, uploads, and deploys from a
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
        ValidateRepository(repository);
        return builder.AddGitHubPages(name,
            builder.AddViteApp($"{name}-vite", appDirectory).WithNpm(installCommand: "ci"), repository);
    }

    /// <summary>
    /// Publishes an existing resource's built container files to GitHub Pages.
    /// </summary>
    /// <param name="builder">The application builder containing the source resource.</param>
    /// <param name="name">The Pages resource name.</param>
    /// <param name="source">The resource producing static files in a container image.</param>
    /// <param name="repository">The GitHub repository in owner/name form.</param>
    /// <returns>The Pages resource builder.</returns>
    /// <remarks>
    /// The source uses Aspire's standard container build pipeline. Pages publishing copies
    /// its declared container files without starting the container or pushing its image.
    /// A source must declare exactly one container files directory.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// var site = builder.AddViteApp("site", "./src/site").WithNpm(installCommand: "ci");
    /// builder.AddGitHubPages("pages", site, "mitchdenny/hex1b");
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<GitHubPagesResource> AddGitHubPages(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<IResourceWithContainerFiles> source,
        string repository)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name is "." or ".." || name.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(name))
        {
            throw new ArgumentException("The Pages resource name must be a single directory name.", nameof(name));
        }
        ArgumentNullException.ThrowIfNull(source);
        if (!ReferenceEquals(source.ApplicationBuilder, builder))
        {
            throw new ArgumentException("The source must belong to the same AppHost.", nameof(source));
        }
        ValidateRepository(repository);

        var resource = new GitHubPagesResource(
            name, source.Resource, repository,
            Path.Combine(builder.AppHostDirectory, ".aspire", "github-pages", name, "dist"));
        var helperDirectory = Path.Combine(builder.AppHostDirectory, ".github", "scripts", "github-pages");
        var resourceBuilder = builder.AddResource(resource)
            .PublishWithContainerFiles(source, resource.BundleDirectory);

        return resourceBuilder.WithPipelineStepFactory(_ => new[]
        {
            new PipelineStep
            {
                Name = $"extract-{name}",
                Description = "Extract and validate the static files from the built container image.",
                Action = async context =>
                {
                    var imageResource = context.Model.Resources.Single(r => r.Name == source.Resource.Name);
                    if (!imageResource.TryGetContainerImageName(out var image))
                    {
                        throw new InvalidOperationException($"Source '{imageResource.Name}' has no built container image.");
                    }
                    var files = imageResource.Annotations.OfType<ContainerFilesSourceAnnotation>().ToArray();
                    if (files.Length != 1 || !files[0].SourcePath.StartsWith('/'))
                    {
                        throw new InvalidOperationException(
                            $"Source '{imageResource.Name}' must declare exactly one absolute container files directory.");
                    }
                    var runtime = await context.Services.GetRequiredService<IContainerRuntimeResolver>()
                        .ResolveAsync(context.CancellationToken);
                    var executable = runtime.Name switch
                    {
                        "Docker" => "docker",
                        "Podman" => "podman",
                        _ => throw new NotSupportedException($"Container runtime '{runtime.Name}' cannot extract Pages files.")
                    };
                    using var staging = context.Services.GetRequiredService<IFileSystemService>()
                        .TempDirectory.CreateTempSubdirectory("github-pages");
                    var bundle = Path.Combine(staging.Path, "dist");
                    Directory.CreateDirectory(bundle);
                    await PagesContainerFiles.ExtractAsync(
                        (arguments, token) => PagesProcess.RunAsync(executable, arguments,
                            helperDirectory, context.Logger, token),
                        image, files[0].SourcePath, bundle, context.CancellationToken);
                    await PagesProcess.RunAsync("node",
                        ["check-bundle.mjs", bundle, $"/{repository.Split('/')[1]}/"],
                        helperDirectory, context.Logger, context.CancellationToken);
                    context.CancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.GetDirectoryName(resource.BundleDirectory)!);
                    if (Directory.Exists(resource.BundleDirectory))
                    {
                        Directory.Delete(resource.BundleDirectory, recursive: true);
                    }
                    Directory.CreateDirectory(resource.BundleDirectory);
                    foreach (var file in Directory.EnumerateFiles(bundle, "*", SearchOption.AllDirectories))
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var destination = Path.Combine(resource.BundleDirectory, Path.GetRelativePath(bundle, file));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(file, destination);
                    }
                }
            },
            new PipelineStep
            {
                Name = $"deploy-{name}",
                Description = "Upload and deploy the static bundle to GitHub Pages.",
                DependsOnSteps = [$"extract-{name}"],
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
        }).WithPipelineConfiguration(context =>
        {
            if (builder.ExecutionContext.IsPublishMode)
            {
                var buildSteps = context.GetSteps(source.Resource, WellKnownPipelineTags.BuildCompute).ToArray();
                if (buildSteps.Length == 0)
                {
                    throw new InvalidOperationException($"Source '{source.Resource.Name}' has no container build step.");
                }
                var extraction = context.Steps.Single(step => step.Name == $"extract-{name}");
                foreach (var build in buildSteps)
                {
                    extraction.DependsOn(build.Name);
                }
            }
        });
    }

    private static void ValidateRepository(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        if (!Regex.IsMatch(repository, @"^[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+$") ||
            repository.Split('/')[1] is "." or "..")
        {
            throw new ArgumentException("The repository must use owner/name form.", nameof(repository));
        }
    }
}
