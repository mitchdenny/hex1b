namespace Aspire.Hosting;

internal static class PagesContainerFiles
{
    internal static async Task ExtractAsync(
        Func<string[], CancellationToken, Task> execute,
        string image, string sourcePath, string destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = $"aspire-pages-{Guid.NewGuid():N}";
        Exception? extractionError = null;
        try
        {
            await execute(["create", "--pull", "never", "--name", container, "--entrypoint", "/bin/true", image],
                cancellationToken);
            await execute(["cp", $"{container}:{sourcePath.TrimEnd('/')}/.", destination],
                cancellationToken);
        }
        catch (Exception error)
        {
            extractionError = error;
            throw;
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await execute(["rm", "--force", "--volumes", container], cleanup.Token);
            }
            catch (Exception cleanupError) when (extractionError is not null)
            {
                throw new AggregateException("Pages extraction and container cleanup both failed.",
                    extractionError, cleanupError);
            }
        }
    }
}
