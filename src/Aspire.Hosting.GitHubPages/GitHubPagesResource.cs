using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Describes a Vite site published through GitHub Actions to GitHub Pages.
/// </summary>
public sealed class GitHubPagesResource : Resource
{
    internal GitHubPagesResource(string name, string sourceDirectory, string repository)
        : base(name)
    {
        SourceDirectory = sourceDirectory;
        Repository = repository;
        var parts = repository.Split('/');
        PublishedUrl = new Uri($"https://{parts[0]}.github.io/{parts[1]}/");
    }

    /// <summary>Gets the absolute directory containing the Vite project.</summary>
    public string SourceDirectory { get; }

    /// <summary>Gets the destination repository in owner/name form.</summary>
    public string Repository { get; }

    /// <summary>Gets the directory containing the production bundle.</summary>
    public string BundleDirectory => Path.Combine(SourceDirectory, "dist");

    /// <summary>Gets the destination project-site URL, without a custom domain.</summary>
    public Uri PublishedUrl { get; }
}
