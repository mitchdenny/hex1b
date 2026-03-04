namespace Hex1b;

/// <summary>
/// Options for configuring a Docker container as a terminal workload.
/// </summary>
/// <remarks>
/// <para>
/// Use either <see cref="Image"/> or <see cref="DockerfilePath"/> to specify
/// the container image, but not both. When <see cref="DockerfilePath"/> is set,
/// the image is built automatically before starting the container.
/// </para>
/// <para>
/// The container runs interactively with a PTY attached, executing the
/// configured <see cref="Shell"/> with <see cref="ShellArgs"/>. All Hex1b
/// terminal features (input sequences, pattern searching, recording) work
/// unchanged because the container is driven through the existing PTY infrastructure.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithHeadless()
///     .WithDockerContainer(c =>
///     {
///         c.Image = "ubuntu:24.04";
///         c.Environment["MY_VAR"] = "value";
///         c.MountDockerSocket = true;
///     })
///     .Build();
/// </code>
/// </example>
public sealed class DockerContainerOptions
{
    /// <summary>
    /// Gets or sets the Docker image to use for the container.
    /// </summary>
    /// <remarks>
    /// Mutually exclusive with <see cref="DockerfilePath"/>. If both are set
    /// to non-default values, validation will throw <see cref="InvalidOperationException"/>.
    /// </remarks>
    public string Image { get; set; } = DockerContainerArgBuilder.DefaultImage;

    /// <summary>
    /// Gets or sets the path to a Dockerfile to build before starting the container.
    /// </summary>
    /// <remarks>
    /// When set, <c>docker build</c> is run before <c>docker run</c>. The built image
    /// is tagged with a content hash of the Dockerfile so rebuilds are skipped when
    /// the file hasn't changed. Mutually exclusive with setting <see cref="Image"/>
    /// to a non-default value.
    /// </remarks>
    public string? DockerfilePath { get; set; }

    /// <summary>
    /// Gets or sets the build context directory for <c>docker build</c>.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="DockerfilePath"/> is set. If null, defaults to
    /// the directory containing the Dockerfile.
    /// </remarks>
    public string? BuildContext { get; set; }

    /// <summary>
    /// Gets the build arguments passed to <c>docker build --build-arg</c>.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="DockerfilePath"/> is set.
    /// </remarks>
    public Dictionary<string, string> BuildArgs { get; } = new();

    /// <summary>
    /// Gets the environment variables passed to the container via <c>docker run -e</c>.
    /// </summary>
    public Dictionary<string, string> Environment { get; } = new();

    /// <summary>
    /// Gets the volume mounts passed to the container via <c>docker run -v</c>.
    /// </summary>
    /// <remarks>
    /// Each entry should be in the format <c>host_path:container_path[:options]</c>.
    /// </remarks>
    public List<string> Volumes { get; } = new();

    /// <summary>
    /// Gets or sets whether to mount the Docker socket into the container.
    /// </summary>
    /// <remarks>
    /// When true, adds <c>-v /var/run/docker.sock:/var/run/docker.sock</c> to enable
    /// Docker-in-Docker scenarios.
    /// </remarks>
    public bool MountDockerSocket { get; set; }

    /// <summary>
    /// Gets or sets an explicit name for the container.
    /// </summary>
    /// <remarks>
    /// If null, a name is auto-generated as <c>hex1b-test-{guid}</c> to enable
    /// cleanup of orphaned containers.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the working directory inside the container.
    /// </summary>
    /// <remarks>
    /// Maps to <c>docker run -w</c>. If null, the container's default working
    /// directory is used.
    /// </remarks>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets the shell to execute inside the container.
    /// </summary>
    public string Shell { get; set; } = "/bin/bash";

    /// <summary>
    /// Gets or sets the arguments passed to the shell.
    /// </summary>
    public string[] ShellArgs { get; set; } = ["--norc"];

    /// <summary>
    /// Gets or sets whether to automatically remove the container when it exits.
    /// </summary>
    /// <remarks>
    /// Maps to <c>docker run --rm</c>. Defaults to true.
    /// </remarks>
    public bool AutoRemove { get; set; } = true;

    /// <summary>
    /// Gets or sets the Docker network to connect the container to.
    /// </summary>
    /// <remarks>
    /// Maps to <c>docker run --network</c>. If null, Docker's default network is used.
    /// </remarks>
    public string? Network { get; set; }
}
