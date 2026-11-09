using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

internal sealed class ScenarioReport
{
    public required string Name { get; init; }
    public required string ArtifactDirectory { get; init; }
    public string? Revision { get; set; }
    public bool? Dirty { get; set; }
    public string? Sdk { get; set; }
    public string? IlCompilerPackage { get; set; }
    public string Host { get; } = RuntimeInformation.OSDescription;
    public string? Rid { get; set; }
    public string Tfm { get; } = "net10.0";
    public string Configuration { get; } = "Release";
    public bool PublishAot { get; } = true;
    public string OptimizationPreference { get; } = "Size";
    public bool IlcGenerateMapFile { get; } = true;
    public string? ExecutablePath { get; set; }
    public long? ExecutableBytes { get; set; }
    public string? ExecutableSha256 { get; set; }
    public string LibraryReference { get; } = "project:src/Hex1b/Hex1b.csproj (PackageId=Hex1b; no published Hex1b package)";
    public string? LibraryInputPath { get; set; }
    public string? LibraryInputSha256 { get; set; }
    public string? LibraryAssemblyIdentity { get; set; }
    public string? LibraryProjectSha256 { get; set; }
    public string? ConsumerSourceSha256 { get; set; }
    public string? Experiment { get; set; }
    public IReadOnlyList<PublishedFile>? SourceOverrides { get; set; }
    public string? CompilerMapPath { get; set; }
    public int? MethodCodeCount { get; set; }
    public long? MethodCodeLength { get; set; }
    public IReadOnlyList<CodeEvidence>? CodeFamilies { get; set; }
    public IReadOnlyList<PublishedFile>? PublishInventory { get; set; }
    public ProcessOutcome? PublishOutcome { get; set; }
    public ProcessOutcome? RuntimeOutcome { get; set; }
    public string Status { get; set; } = "started";
    public string? Failure { get; set; }
    public IReadOnlyList<string> EvidencePaths { get; set; } = [];
}

internal sealed record PublishedFile(string Path, string Kind, long Bytes, string Sha256);

internal static class ScenarioRunner
{
    private static readonly string RunId = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
    public static string RepositoryRoot => FindRepositoryRoot();

    private static string FindRepositoryRoot([CallerFilePath] string source = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));

    public static string HostRid()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Native AOT scenarios require an x64 or arm64 process, got {RuntimeInformation.ProcessArchitecture}.")
        };
        var platform = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsMacOS() ? "osx" :
            OperatingSystem.IsLinux() ? (RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl-", StringComparison.Ordinal) ? "linux-musl" : "linux") :
            throw new PlatformNotSupportedException($"Unsupported Native AOT host: {RuntimeInformation.OSDescription}.");
        return $"{platform}-{architecture}";
    }

    public static string CreateArtifacts(string name)
    {
        var directory = Path.Combine(RepositoryRoot, "TestResults", "NativeAot", RunId, name);
        if (Directory.Exists(directory))
            throw new InvalidOperationException($"Scenario already ran in this invocation; refusing to overwrite evidence at {directory}.");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static async Task<ScenarioReport> RunAsync(Scenario scenario, string source, TestContext context,
        CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? sourceOverrides = null,
        string? experiment = null)
    {
        var directory = CreateArtifacts(scenario.Name);
        var report = new ScenarioReport { Name = scenario.Name, ArtifactDirectory = directory, Experiment = experiment };
        var phase = "preflight";
        try
        {
            report.Rid = HostRid();
            var revision = await TestProcess.RunAsync("git", ["rev-parse", "HEAD"], RepositoryRoot,
                Path.Combine(directory, "revision.log"), TimeSpan.FromSeconds(30), cancellationToken);
            report.Revision = revision.StandardOutput.Trim();
            var status = await TestProcess.RunAsync("git", ["status", "--porcelain"], RepositoryRoot,
                Path.Combine(directory, "git-status.log"), TimeSpan.FromSeconds(30), cancellationToken);
            report.Dirty = !string.IsNullOrWhiteSpace(status.StandardOutput);
            var sdk = await TestProcess.RunAsync("dotnet", ["--version"], RepositoryRoot,
                Path.Combine(directory, "sdk.log"), TimeSpan.FromSeconds(30), cancellationToken);
            report.Sdk = sdk.StandardOutput.Trim();
            report.LibraryProjectSha256 = Hash(Path.Combine(RepositoryRoot, "src", "Hex1b", "Hex1b.csproj"));
            var project = await CreateProjectAsync(directory, scenario.ConsumerName, source, cancellationToken, sourceOverrides);
            report.ConsumerSourceSha256 = Hash(Path.Combine(directory, "source", "Program.cs"));
            if (sourceOverrides is not null)
                report.SourceOverrides = sourceOverrides.Keys.Select(name =>
                {
                    var path = Path.Combine(directory, "source", "overrides", name);
                    return new PublishedFile(name, "experimental library source", new FileInfo(path).Length, Hash(path));
                }).ToArray();
            phase = "publish";
            report.PublishOutcome = await TestProcess.RunAsync("dotnet",
                ["publish", project, "-c", "Release", "-r", report.Rid, "--self-contained", "true",
                 "-p:BuildInParallel=false", "-m:1", "-v:minimal",
                 "-bl:" + Path.Combine(directory, "publish.binlog")],
                RepositoryRoot, Path.Combine(directory, "publish.log"), TimeSpan.FromMinutes(10), cancellationToken);
            phase = "inspect";
            using (var assets = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(directory, "obj", "project.assets.json"), cancellationToken)))
            {
                report.IlCompilerPackage = assets.RootElement.GetProperty("libraries").EnumerateObject()
                    .Single(property => property.Name.StartsWith("Microsoft.DotNet.ILCompiler/", StringComparison.Ordinal)).Name;
            }
            var publish = Path.Combine(directory, "publish");
            var executable = Path.Combine(publish, scenario.ConsumerName + (OperatingSystem.IsWindows() ? ".exe" : ""));
            report.ExecutablePath = executable;
            if (!File.Exists(executable) || new FileInfo(executable).Length == 0)
                throw new InvalidDataException($"Missing or empty native executable: {executable}.");
            report.ExecutableBytes = new FileInfo(executable).Length;
            report.ExecutableSha256 = Hash(executable);
            report.PublishInventory = Directory.EnumerateFiles(publish, "*", SearchOption.AllDirectories)
                .Select(path => new PublishedFile(Path.GetRelativePath(publish, path), FileKind(path, executable),
                    new FileInfo(path).Length, Hash(path))).OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
            if (File.Exists(Path.Combine(publish, scenario.ConsumerName + ".dll")))
                throw new InvalidDataException("Publish contains the managed consumer entry assembly; expected Native AOT output only.");

            var library = Directory.GetFiles(Path.Combine(directory, "bin"), "Hex1b.dll", SearchOption.AllDirectories);
            if (library.Length != 1)
                throw new InvalidDataException($"Expected one untrimmed Hex1b compiler input in the consumer build output; found {library.Length}.");
            report.LibraryInputPath = library[0];
            report.LibraryInputSha256 = Hash(library[0]);
            report.LibraryAssemblyIdentity = AssemblyName.GetAssemblyName(library[0]).FullName;
            var maps = Directory.GetFiles(Path.Combine(directory, "obj"), "*.map.xml", SearchOption.AllDirectories);
            if (maps.Length != 1)
                throw new InvalidDataException($"Expected exactly one native compiler .map.xml; found {maps.Length} in {directory}.");
            report.CompilerMapPath = maps[0];
            var methods = NativeMap.Read(maps[0]);
            report.MethodCodeCount = methods.Count;
            report.MethodCodeLength = methods.Sum(method => method.Length);
            report.CodeFamilies = NativeMap.Measure(methods, scenario.Required, scenario.Forbidden);
            NativeMap.Validate(report.CodeFamilies);

            phase = "runtime";
            report.RuntimeOutcome = scenario.UsePty
                ? await TestProcess.RunPtyAsync(executable, publish, Path.Combine(directory, "runtime.log"),
                    scenario.PtyStopPrompts, cancellationToken)
                : await TestProcess.RunAsync(executable, [], publish,
                    Path.Combine(directory, "runtime.log"), TimeSpan.FromSeconds(30), cancellationToken);
            var marker = "NATIVE_AOT_OK:" + scenario.ConsumerName;
            var foundMarker = scenario.UsePty
                ? report.RuntimeOutcome.StandardOutput.Contains(marker, StringComparison.Ordinal)
                : report.RuntimeOutcome.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Contains(marker, StringComparer.Ordinal);
            if (!foundMarker)
                throw new InvalidDataException($"Native program exited without its verified success marker; see {report.RuntimeOutcome.LogPath}.");
            report.Status = "passed";
        }
        catch (Exception ex)
        {
            if (ex is TestProcessException processFailure)
            {
                if (phase == "publish")
                    report.PublishOutcome = processFailure.Outcome;
                else if (phase == "runtime")
                    report.RuntimeOutcome = processFailure.Outcome;
            }
            report.Status = "failed";
            report.Failure = ex.ToString();
            throw new InvalidOperationException(
                $"{scenario.Name} Native AOT scenario failed. Native prerequisites are required (see tests/Hex1b.Tests/NativeAot/README.md).\n" +
                $"Evidence retained at {directory}.\n{ex.Message}", ex);
        }
        finally
        {
            var reportPath = Path.Combine(directory, "report.json");
            var sourceDirectory = Path.Combine(directory, "source");
            var sourceFiles = Directory.Exists(sourceDirectory) ? Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories) : [];
            var snapshots = Directory.GetFiles(directory, "*.svg");
            report.EvidencePaths = Directory.EnumerateFiles(directory, "*.log")
                .Concat(Directory.EnumerateFiles(directory, "*.binlog"))
                .Concat(sourceFiles)
                .Concat(snapshots)
                .Concat(report.CompilerMapPath is null ? [] : new[] { report.CompilerMapPath })
                .Append(reportPath).ToArray();
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(reportPath, json, CancellationToken.None);
            context.WriteLine(json);
            context.AddResultFile(reportPath);
            foreach (var log in Directory.EnumerateFiles(directory, "*.log"))
                context.AddResultFile(log);
            foreach (var sourceFile in sourceFiles)
                context.AddResultFile(sourceFile);
            foreach (var snapshot in snapshots)
                context.AddResultFile(snapshot);
        }
        return report;
    }

    public static async Task<string> CreateProjectAsync(string directory, string name, string source,
        CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? sourceOverrides = null)
    {
        var sourceDirectory = Path.Combine(directory, "source");
        Directory.CreateDirectory(sourceDirectory);
        var props = new XElement("Project",
            new XElement("Import", new XAttribute("Project", Path.Combine(RepositoryRoot, "Directory.Build.props"))),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("OutputType", "Exe"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("Nullable", "enable"),
                new XElement("TreatWarningsAsErrors", "true"),
                new XElement("IsPackable", "false"),
                new XElement("PublishAot", "true"),
                new XElement("OptimizationPreference", "Size"),
                new XElement("IlcGenerateMapFile", "true"),
                new XElement("BaseIntermediateOutputPath", Path.Combine(directory, "obj") + Path.DirectorySeparatorChar),
                new XElement("BaseOutputPath", Path.Combine(directory, "bin") + Path.DirectorySeparatorChar),
                new XElement("PublishDir", Path.Combine(directory, "publish") + Path.DirectorySeparatorChar)));
        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include",
                    Path.Combine(RepositoryRoot, "src", "Hex1b", "Hex1b.csproj")))));
        if (sourceOverrides is not null)
        {
            var overridesDirectory = Path.Combine(sourceDirectory, "overrides");
            Directory.CreateDirectory(overridesDirectory);
            // Compile replacements must precede the SDK's incremental input cache.
            var target = new XElement("Target", new XAttribute("Name", "NativeAotExperimentalSources"),
                new XAttribute("BeforeTargets", "AssignTargetPaths"),
                new XAttribute("Condition", "'$(MSBuildProjectName)' == 'Hex1b'"));
            var items = new XElement("ItemGroup",
                new XElement("_NativeAotCompile", new XAttribute("Include", "@(Compile)"),
                    new XElement("ReplacementPath", "%(Compile.Identity)")));
            foreach (var (file, content) in sourceOverrides)
            {
                if (Path.GetFileName(file) != file || !File.Exists(Path.Combine(RepositoryRoot, "src", "Hex1b", file)))
                    throw new InvalidDataException($"Experimental overrides must name existing root library source files: {file}.");
                var path = Path.Combine(overridesDirectory, file);
                await File.WriteAllTextAsync(path, content, cancellationToken);
                items.Add(new XElement("_NativeAotCompile", new XAttribute("Condition", $"'%(_NativeAotCompile.Identity)' == '{file}'"),
                    new XElement("ReplacementPath", path), new XElement("Link", file)));
            }
            // Partial-type field layout depends on source order. Replace paths in place
            // rather than moving entire construction files to the end of Compile.
            items.Add(new XElement("Compile", new XAttribute("Remove", "@(Compile)")));
            items.Add(new XElement("Compile", new XAttribute("Include", "@(_NativeAotCompile->'%(ReplacementPath)')")));
            items.Add(new XElement("_NativeAotCompile", new XAttribute("Remove", "@(_NativeAotCompile)")));
            target.Add(items);
            var targetsPath = Path.Combine(sourceDirectory, "ExperimentalSources.targets");
            await File.WriteAllTextAsync(targetsPath, new XElement("Project", target).ToString(), cancellationToken);
            project.Element("ItemGroup")!.Element("ProjectReference")!.Add(
                new XElement("AdditionalProperties", "DirectoryBuildTargetsPath=" + targetsPath));
            project.Element("ItemGroup")!.Add(new XElement("Compile",
                new XAttribute("Remove", Path.Combine("overrides", "**", "*.cs"))));
        }
        var projectPath = Path.Combine(sourceDirectory, name + ".csproj");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Directory.Build.props"), props.ToString(), cancellationToken);
        await File.WriteAllTextAsync(projectPath, project.ToString(), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Program.cs"), source, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "EchoWorkload.cs"), EchoWorkloadSource, cancellationToken);
        return projectPath;
    }

    private const string EchoWorkloadSource = """
        using System.Text;
        using System.Threading.Channels;
        using Hex1b;

        namespace AotScenarios;

        internal sealed class EchoWorkload : IHex1bTerminalWorkloadAdapter
        {
            private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
            private readonly StringBuilder _input = new();

            public bool IsDisposed { get; private set; }
            public string Input => _input.ToString();
            public event Action? Disconnected;

            public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
                => _output.Reader.ReadAsync(ct);

            public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                _input.Append(Encoding.UTF8.GetString(data.Span));
                return _output.Writer.WriteAsync(data.ToArray(), ct);
            }

            public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
                => ValueTask.CompletedTask;

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                _output.Writer.TryComplete();
                Disconnected?.Invoke();
                return ValueTask.CompletedTask;
            }
        }
        """;

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string FileKind(string path, string executable)
        => path == executable ? "main executable" :
            Path.GetExtension(path) is ".pdb" or ".dbg" or ".map" or ".lib" or ".exp" ||
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.EndsWith(".dSYM", StringComparison.Ordinal)) ? "symbols/compiler support" :
            Path.GetExtension(path) == ".xml" ? "documentation" :
            Path.GetFileName(path).StartsWith("hex1bpty", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).StartsWith("libhex1binterop", StringComparison.Ordinal) ? "native helper/support" :
            "runtime support";
}
