using Hex1b.Terminal.Testing;

namespace Hex1b.Tests;

/// <summary>
/// Helper class for capturing and attaching terminal SVG/HTML/ANSI snapshots to test results.
/// </summary>
public static class TestSvgHelper
{
    /// <summary>
    /// Captures an SVG snapshot of the terminal and attaches it to the test context.
    /// </summary>
    /// <param name="terminal">The terminal to capture.</param>
    /// <param name="name">Name for the SVG file (without extension). Must be unique within a test.</param>
    public static void CaptureSvg(Hex1bTerminal terminal, string name = "snapshot")
    {
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        AttachSvg($"{name}.svg", svg);
    }

    /// <summary>
    /// Captures an SVG snapshot of the terminal snapshot and attaches it to the test context.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="name">Name for the SVG file (without extension). Must be unique within a test.</param>
    public static void CaptureSvg(Hex1bTerminalSnapshot snapshot, string name = "snapshot")
    {
        var svg = snapshot.ToSvg();
        AttachSvg($"{name}.svg", svg);
    }

    /// <summary>
    /// Captures an interactive HTML snapshot of the terminal with cell inspection.
    /// </summary>
    /// <param name="terminal">The terminal to capture.</param>
    /// <param name="name">Name for the HTML file (without extension). Must be unique within a test.</param>
    public static void CaptureHtml(Hex1bTerminal terminal, string name = "snapshot")
    {
        var snapshot = terminal.CreateSnapshot();
        var html = snapshot.ToHtml();
        AttachFile($"{name}.html", html);
    }

    /// <summary>
    /// Captures an interactive HTML snapshot of the terminal snapshot with cell inspection.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="name">Name for the HTML file (without extension). Must be unique within a test.</param>
    public static void CaptureHtml(Hex1bTerminalSnapshot snapshot, string name = "snapshot")
    {
        var html = snapshot.ToHtml();
        AttachFile($"{name}.html", html);
    }

    /// <summary>
    /// Captures an ANSI escape code representation of the terminal.
    /// The output can be piped to a terminal with `cat` to display the captured state.
    /// </summary>
    /// <param name="terminal">The terminal to capture.</param>
    /// <param name="name">Name for the ANSI file (without extension). Must be unique within a test.</param>
    public static void CaptureAnsi(Hex1bTerminal terminal, string name = "snapshot")
    {
        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi();
        AttachFile($"{name}.ansi", ansi);
    }

    /// <summary>
    /// Captures an ANSI escape code representation of the terminal snapshot.
    /// The output can be piped to a terminal with `cat` to display the captured state.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="name">Name for the ANSI file (without extension). Must be unique within a test.</param>
    public static void CaptureAnsi(Hex1bTerminalSnapshot snapshot, string name = "snapshot")
    {
        var ansi = snapshot.ToAnsi();
        AttachFile($"{name}.ansi", ansi);
    }

    /// <summary>
    /// Captures SVG, HTML, and ANSI snapshots of the terminal.
    /// </summary>
    /// <param name="terminal">The terminal to capture.</param>
    /// <param name="name">Base name for the files (without extension). Must be unique within a test.</param>
    public static void Capture(Hex1bTerminal terminal, string name = "snapshot")
    {
        CaptureSvg(terminal, name);
        CaptureHtml(terminal, name);
        CaptureAnsi(terminal, name);
    }

    /// <summary>
    /// Captures SVG, HTML, and ANSI snapshots of the terminal snapshot.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="name">Base name for the files (without extension). Must be unique within a test.</param>
    public static void Capture(Hex1bTerminalSnapshot snapshot, string name = "snapshot")
    {
        CaptureSvg(snapshot, name);
        CaptureHtml(snapshot, name);
        CaptureAnsi(snapshot, name);
    }

    /// <summary>
    /// Attaches an SVG string to the test context as a proper test attachment.
    /// In xUnit v3, attachments are recorded in test results XML/JSON.
    /// Also writes to disk for easy viewing.
    /// </summary>
    /// <param name="name">The filename for the SVG (should include .svg extension). Must be unique within a test.</param>
    /// <param name="svg">The SVG content.</param>
    /// <exception cref="ArgumentException">Thrown if an attachment with the same name already exists.</exception>
    public static void AttachSvg(string name, string svg)
    {
        AttachFile(name, svg);
    }

    /// <summary>
    /// Attaches a file to the test context.
    /// </summary>
    /// <param name="name">The filename (with extension). Must be unique within a test.</param>
    /// <param name="content">The file content.</param>
    public static void AttachFile(string name, string content)
    {
        // Attach to xUnit test context - will throw if name is duplicate
        TestContext.Current.AddAttachment(name, content);

        // Build path: TestResults/{type}/{TestClass}_{TestName}_{attachment}
        var testContext = TestContext.Current;
        var testClass = testContext.Test?.TestCase?.TestClassName ?? "UnknownClass";
        var testMethodName = testContext.Test?.TestCase?.TestMethod?.MethodName ?? "UnknownMethod";
        
        // Get test display name which includes theory parameters
        var testDisplayName = testContext.Test?.TestDisplayName ?? testMethodName;
        
        // Extract just the method part with parameters if it's a theory
        var methodWithParams = testDisplayName;
        if (methodWithParams.Contains('.'))
        {
            methodWithParams = methodWithParams[(methodWithParams.LastIndexOf('.') + 1)..];
        }

        // Sanitize for filesystem
        var sanitizedClass = SanitizeFileName(testClass);
        var sanitizedMethod = SanitizeFileName(methodWithParams);

        // Determine output subdirectory based on file type
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var subDir = extension switch
        {
            ".html" => "html",
            ".svg" => "svg",
            ".ansi" => "ansi",
            _ => "files"
        };

        // Build output path - flattened structure with _ separator instead of subdirectories
        var assemblyDir = Path.GetDirectoryName(typeof(TestSvgHelper).Assembly.Location)!;
        var outputDir = Path.Combine(assemblyDir, "TestResults", subDir);
        Directory.CreateDirectory(outputDir);
        
        // Flatten: {TestClass}_{TestMethod}_{attachment}
        var flattenedName = $"{sanitizedClass}_{sanitizedMethod}_{name}";
        var filePath = Path.Combine(outputDir, flattenedName);
        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Sanitizes a string for use as a file or directory name.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder(name.Length);
        
        foreach (var c in name)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
            {
                result.Append('_');
            }
            else if (c == '"')
            {
                result.Append('\'');
            }
            else
            {
                result.Append(c);
            }
        }
        
        // Truncate if too long (some filesystems have limits)
        var sanitized = result.ToString();
        if (sanitized.Length > 200)
        {
            sanitized = sanitized[..200];
        }
        
        return sanitized;
    }
}
