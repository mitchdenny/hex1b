using System.Security.Cryptography;
using System.Text;

namespace Hex1b;

/// <summary>
/// Builds command-line arguments for <c>docker run</c> and <c>docker build</c> commands.
/// </summary>
internal static class DockerContainerArgBuilder
{
    internal const string DefaultImage = "mcr.microsoft.com/dotnet/sdk:10.0";
    internal const string ContainerNamePrefix = "hex1b-test-";
    internal const string ImageTagPrefix = "hex1b-test-";

    /// <summary>
    /// Validates the <see cref="DockerContainerOptions"/> for consistency.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are in an invalid state.
    /// </exception>
    public static void Validate(DockerContainerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool hasCustomImage = options.Image != DefaultImage;
        bool hasDockerfile = options.DockerfilePath is not null;

        if (hasCustomImage && hasDockerfile)
        {
            throw new InvalidOperationException(
                "Set Image or DockerfilePath, not both. " +
                "When DockerfilePath is set, the image is built from the Dockerfile.");
        }

        if (!hasDockerfile && options.BuildContext is not null)
        {
            throw new InvalidOperationException(
                "BuildContext can only be set when DockerfilePath is specified.");
        }

        if (!hasDockerfile && options.BuildArgs.Count > 0)
        {
            throw new InvalidOperationException(
                "BuildArgs can only be set when DockerfilePath is specified.");
        }
    }

    /// <summary>
    /// Builds the arguments for a <c>docker run</c> command.
    /// </summary>
    /// <param name="options">The container configuration options.</param>
    /// <param name="containerName">The name to assign to the container.</param>
    /// <returns>An array of command-line arguments.</returns>
    public static string[] BuildRunArgs(DockerContainerOptions options, string containerName)
    {
        var args = new List<string> { "run", "-it" };

        if (options.AutoRemove)
        {
            args.Add("--rm");
        }

        args.Add("--name");
        args.Add(containerName);

        foreach (var (key, value) in options.Environment)
        {
            args.Add("-e");
            args.Add($"{key}={value}");
        }

        foreach (var volume in options.Volumes)
        {
            args.Add("-v");
            args.Add(volume);
        }

        if (options.MountDockerSocket)
        {
            args.Add("-v");
            args.Add("/var/run/docker.sock:/var/run/docker.sock");
        }

        if (options.WorkingDirectory is not null)
        {
            args.Add("-w");
            args.Add(options.WorkingDirectory);
        }

        if (options.Network is not null)
        {
            args.Add("--network");
            args.Add(options.Network);
        }

        args.AddRange(options.AdditionalRunArgs);

        args.Add(options.Image);
        args.Add(options.Shell);

        foreach (var shellArg in options.ShellArgs)
        {
            args.Add(shellArg);
        }

        return args.ToArray();
    }

    /// <summary>
    /// Builds the arguments for a <c>docker build</c> command.
    /// </summary>
    /// <param name="options">The container configuration options.</param>
    /// <param name="contentHash">A hash of the Dockerfile content for tagging.</param>
    /// <returns>A tuple of (arguments array, image tag).</returns>
    public static (string[] Args, string ImageTag) BuildBuildArgs(DockerContainerOptions options, string contentHash)
    {
        var imageTag = $"{ImageTagPrefix}{contentHash}";

        var buildContext = options.BuildContext
            ?? Path.GetDirectoryName(options.DockerfilePath)
            ?? ".";

        var args = new List<string> { "build" };

        args.Add("-f");
        args.Add(options.DockerfilePath!);

        args.Add("-t");
        args.Add(imageTag);

        foreach (var (key, value) in options.BuildArgs)
        {
            args.Add("--build-arg");
            args.Add($"{key}={value}");
        }

        args.Add(buildContext);

        return (args.ToArray(), imageTag);
    }

    /// <summary>
    /// Computes a truncated SHA-256 hash of Dockerfile content for image tagging.
    /// </summary>
    /// <param name="content">The Dockerfile content.</param>
    /// <returns>A 12-character hex string.</returns>
    public static string ComputeDockerfileHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Generates a unique container name.
    /// </summary>
    /// <returns>A name in the format <c>hex1b-test-{guid}</c>.</returns>
    public static string GenerateContainerName()
    {
        return $"{ContainerNamePrefix}{Guid.NewGuid():N}"[..32];
    }
}
