using System.Diagnostics;

namespace Hex1b;

/// <summary>
/// Extension methods for configuring Docker containers as terminal workloads.
/// </summary>
public static class DockerContainerExtensions
{
    /// <summary>
    /// Configures the terminal to run inside a Docker container.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="configure">An action to configure the Docker container options.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method wraps <see cref="Hex1bTerminalBuilder.WithPtyProcess(string, string[])"/>
    /// under the hood. The Docker container is started with an interactive PTY
    /// (<c>docker run -it</c>), so all terminal features work unchanged.
    /// </para>
    /// <para>
    /// When <see cref="DockerContainerOptions.DockerfilePath"/> is set, the image
    /// is built automatically with content-hash tagging before starting the container.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithHeadless()
    ///     .WithDockerContainer(c =>
    ///     {
    ///         c.Image = "ubuntu:24.04";
    ///         c.Environment["MY_VAR"] = "value";
    ///     })
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured options are invalid (e.g., both Image and DockerfilePath are set).
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when <see cref="DockerContainerOptions.DockerfilePath"/> is set but the file does not exist.
    /// </exception>
    public static Hex1bTerminalBuilder WithDockerContainer(
        this Hex1bTerminalBuilder builder,
        Action<DockerContainerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DockerContainerOptions();
        configure(options);

        return ConfigureDockerContainer(builder, options);
    }

    /// <summary>
    /// Configures the terminal to run inside a Docker container with default options.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// Uses the default image (<c>mcr.microsoft.com/dotnet/sdk:10.0</c>) with no
    /// additional configuration. Equivalent to calling
    /// <c>WithDockerContainer(c => { })</c>.
    /// </remarks>
    public static Hex1bTerminalBuilder WithDockerContainer(
        this Hex1bTerminalBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return ConfigureDockerContainer(builder, new DockerContainerOptions());
    }

    private static Hex1bTerminalBuilder ConfigureDockerContainer(
        Hex1bTerminalBuilder builder,
        DockerContainerOptions options)
    {
        DockerContainerArgBuilder.Validate(options);

        string image = options.Image;

        if (options.DockerfilePath is not null)
        {
            image = BuildDockerImage(options);
            options.Image = image;
        }

        var containerName = options.Name ?? DockerContainerArgBuilder.GenerateContainerName();
        var args = DockerContainerArgBuilder.BuildRunArgs(options, containerName);

        return builder.WithPtyProcess("docker", args);
    }

    private static string BuildDockerImage(DockerContainerOptions options)
    {
        var dockerfilePath = Path.GetFullPath(options.DockerfilePath!);

        if (!File.Exists(dockerfilePath))
        {
            throw new FileNotFoundException(
                $"Dockerfile not found: {dockerfilePath}",
                dockerfilePath);
        }

        var content = File.ReadAllText(dockerfilePath);
        var hash = DockerContainerArgBuilder.ComputeDockerfileHash(content);
        var (buildArgs, imageTag) = DockerContainerArgBuilder.BuildBuildArgs(options, hash);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in buildArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new System.Diagnostics.Process { StartInfo = startInfo, EnableRaisingEvents = true };

        // Use event-based async reading to avoid the classic deadlock where
        // sequential ReadToEnd() calls block when both pipe buffers fill.
        // Docker BuildKit sends all build progress to stderr; on Windows
        // the pipe buffer is only 4 KB and overflows quickly.
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker build failed with exit code {process.ExitCode}: {stderr}");
        }

        return imageTag;
    }
}
