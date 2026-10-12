using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Describes a Vite site published through GitHub Actions to GitHub Pages.
/// </summary>
public sealed class GitHubPagesResource : Resource
{
    internal GitHubPagesResource(string name, IResourceWithContainerFiles source, string repository, string bundleDirectory)
        : base(name)
    {
        Source = source;
        Repository = repository;
        BundleDirectory = bundleDirectory;
        var parts = repository.Split('/');
        PublishedUrl = new Uri($"https://{parts[0]}.github.io/{parts[1]}/");
    }

    /// <summary>Gets the resource producing the static files in a container image.</summary>
    public IResourceWithContainerFiles Source { get; }

    /// <summary>Gets the destination repository in owner/name form.</summary>
    public string Repository { get; }

    /// <summary>Gets the directory containing the extracted and validated production bundle.</summary>
    public string BundleDirectory { get; }

    /// <summary>Gets the destination project-site URL, without a custom domain.</summary>
    public Uri PublishedUrl { get; }
}
