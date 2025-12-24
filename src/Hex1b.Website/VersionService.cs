using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Hex1b.Website;

/// <summary>
/// Interface for retrieving the Hex1b package version.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the current Hex1b package version to display.
    /// </summary>
    string Version { get; }
}

/// <summary>
/// Service that provides version information for the Hex1b package.
/// Runs as a background service to periodically refresh the NuGet version.
/// </summary>
public class VersionService : BackgroundService, IVersionService
{
    private const string CacheKey = "Hex1b:PackageVersion";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(2);

    private readonly IMemoryCache _cache;
    private readonly ILogger<VersionService> _logger;
    private readonly HttpClient _httpClient;

    public VersionService(IMemoryCache cache, ILogger<VersionService> logger, IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("NuGet");
    }

    /// <inheritdoc />
    public string Version => ResolveVersion();

    private string ResolveVersion()
    {
#if DEBUG
        // For debug builds, just use the git tag
        var gitVersion = GetLatestGitTag();
        if (gitVersion != null)
        {
            return gitVersion;
        }
        // Fallback if git is not available
        return "0.1.0-dev";
#else
        // For release builds, check the assembly informational version
        var hex1bAssembly = typeof(Hex1bApp).Assembly;
        var informationalVersion = hex1bAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrEmpty(informationalVersion))
        {
            // Remove the +sha suffix if present (e.g., "0.13.0+abc1234" -> "0.13.0")
            var plusIndex = informationalVersion.IndexOf('+');
            var version = plusIndex > 0
                ? informationalVersion.Substring(0, plusIndex)
                : informationalVersion;

            // If it has a prerelease suffix (contains '-'), use it as-is
            if (version.Contains('-'))
            {
                return version;
            }

            // No prerelease suffix - this is a stable version like "1.0.0"
            // Try to get the cached NuGet version instead
            if (_cache.TryGetValue(CacheKey, out string? cachedVersion) && !string.IsNullOrEmpty(cachedVersion))
            {
                return cachedVersion;
            }

            // If cache is empty but we have a valid version, use it
            if (version != "1.0.0")
            {
                return version;
            }
        }

        // Final fallback
        return "0.1.0";
#endif
    }

#if DEBUG
    private static string? GetLatestGitTag()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "describe --tags --abbrev=0 --match \"v*\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (process.ExitCode == 0 && output.StartsWith("v"))
            {
                // Strip the 'v' prefix and parse the version
                var versionStr = output.Substring(1);
                var parts = versionStr.Split('.');
                
                if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
                {
                    // Increment minor version and add '-dev' suffix
                    return $"{major}.{minor + 1}.0-dev";
                }
                
                // Fallback if parsing fails
                return versionStr + "-dev";
            }
        }
        catch
        {
            // Ignore errors - git might not be available
        }

        return null;
    }
#endif

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        // Don't run the background refresh in debug mode
        return;
#else
        // Initial fetch
        await RefreshNuGetVersionAsync(stoppingToken);

        // Periodic refresh
        using var timer = new PeriodicTimer(RefreshInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RefreshNuGetVersionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh NuGet version");
            }
        }
#endif
    }

    private async Task RefreshNuGetVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(
                "https://api.nuget.org/v3-flatcontainer/hex1b/index.json",
                cancellationToken);

            using var doc = JsonDocument.Parse(response);
            var versions = doc.RootElement.GetProperty("versions");

            // Get the last stable version (no prerelease suffix)
            string? latestStable = null;
            foreach (var version in versions.EnumerateArray())
            {
                var versionStr = version.GetString();
                if (versionStr != null && !versionStr.Contains('-'))
                {
                    latestStable = versionStr;
                }
            }

            if (!string.IsNullOrEmpty(latestStable))
            {
                _cache.Set(CacheKey, latestStable, CacheExpiration);
                _logger.LogInformation("Updated cached NuGet version to {Version}", latestStable);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch latest version from NuGet.org");
        }
    }
}
