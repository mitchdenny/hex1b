namespace Hex1b.Tests.TestHelpers;

/// <summary>
/// A disposable test workspace that creates a temporary directory for test files.
/// </summary>
public sealed class TestWorkspace : IDisposable
{
    private readonly DirectoryInfo _baseDirectory;
    private bool _disposed;

    private TestWorkspace(DirectoryInfo baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Gets the base directory for the test workspace.
    /// </summary>
    public DirectoryInfo BaseDirectory => _baseDirectory;

    /// <summary>
    /// Creates a new test workspace with a unique temporary directory.
    /// </summary>
    /// <param name="prefix">Optional prefix for the directory name.</param>
    /// <returns>A new TestWorkspace instance.</returns>
    public static TestWorkspace Create(string? prefix = null)
    {
        var dirName = prefix != null 
            ? $"{prefix}_{Guid.NewGuid():N}" 
            : $"hex1b_test_{Guid.NewGuid():N}";
        
        var path = Path.Combine(Path.GetTempPath(), dirName);
        var dir = Directory.CreateDirectory(path);
        
        return new TestWorkspace(dir);
    }

    /// <summary>
    /// Creates a file in the workspace with the specified content.
    /// </summary>
    /// <param name="relativePath">Path relative to the workspace base directory.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <returns>FileInfo for the created file.</returns>
    public FileInfo CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_baseDirectory.FullName, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }

    /// <summary>
    /// Creates a C# program file that can be run with 'dotnet run'.
    /// </summary>
    /// <param name="fileName">The file name (e.g., "Program.cs").</param>
    /// <param name="code">The C# code.</param>
    /// <returns>FileInfo for the created file.</returns>
    public FileInfo CreateCSharpProgram(string fileName, string code)
    {
        return CreateFile(fileName, code);
    }

    /// <summary>
    /// Gets the full path for a file in the workspace.
    /// </summary>
    /// <param name="relativePath">Path relative to the workspace base directory.</param>
    /// <returns>The full path.</returns>
    public string GetPath(string relativePath)
    {
        return Path.Combine(_baseDirectory.FullName, relativePath);
    }

    /// <summary>
    /// Creates a subdirectory in the workspace.
    /// </summary>
    /// <param name="relativePath">Path relative to the workspace base directory.</param>
    /// <returns>DirectoryInfo for the created directory.</returns>
    public DirectoryInfo CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_baseDirectory.FullName, relativePath);
        return Directory.CreateDirectory(fullPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_baseDirectory.Exists)
            {
                _baseDirectory.Delete(recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup - don't fail tests if cleanup fails
        }
    }
}
