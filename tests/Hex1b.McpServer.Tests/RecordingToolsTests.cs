using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Hex1b.McpServer.Tests;

/// <summary>
/// Integration tests for the asciinema recording MCP tools.
/// </summary>
[TestClass]
public class RecordingToolsTests : McpServerTestBase
{
    private readonly List<string> _tempFiles = new();

    private static string StartTerminalToolName => OperatingSystem.IsWindows()
        ? "start_pwsh_terminal"
        : "start_bash_terminal";

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcp_recording_test_{Guid.NewGuid():N}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        
        // Clean up temp files
        await Task.Delay(100);
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    private static string? GetTextContent(CallToolResult result)
    {
        var textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        return textBlock?.Text;
    }

    private async Task<string> StartTerminalSessionAsync(McpClient client, int width = 80, int height = 24)
    {
        var createResult = await client.CallToolAsync(
            StartTerminalToolName,
            new Dictionary<string, object?>
            {
                ["width"] = width,
                ["height"] = height,
                ["workingDirectory"] = Path.GetTempPath()
            },
            cancellationToken: TestCancellationToken);

        var createText = GetTextContent(createResult);
        Assert.IsNotNull(createText);

        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var success = createResponse.TryGetProperty("success", out var successValue) && successValue.GetBoolean();
        var message = createResponse.TryGetProperty("message", out var messageValue)
            ? messageValue.GetString()
            : null;

        Assert.IsTrue(success, message ?? $"Expected {StartTerminalToolName} to succeed.");
        Assert.IsTrue(
            createResponse.TryGetProperty("sessionId", out var sessionIdValue)
            && !string.IsNullOrWhiteSpace(sessionIdValue.GetString()),
            $"Expected terminal start response to include a sessionId. Response: {createText}");

        return sessionIdValue.GetString()!;
    }

    [TestMethod]
    public async Task StartAsciinemaRecording_ValidSession_StartsRecording()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        var sessionId = await StartTerminalSessionAsync(client);
        var tempFile = GetTempFile();

        // Act
        var startResult = await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["filePath"] = tempFile
            },
            cancellationToken: TestCancellationToken);

        // Assert
        Assert.IsNotNull(startResult);
        var startText = GetTextContent(startResult);
        Assert.IsNotNull(startText);
        var startResponse = JsonSerializer.Deserialize<JsonElement>(startText);
        Assert.IsTrue(startResponse.GetProperty("success").GetBoolean());
        // File path includes .cast extension enforcement
        Assert.Contains(".cast", startResponse.GetProperty("filePath").GetString());
        
        // Verify file was created
        var actualPath = startResponse.GetProperty("filePath").GetString()!;
        Assert.IsTrue(File.Exists(actualPath), "Recording file should exist");

        // Cleanup - remove the session
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [TestMethod]
    public async Task StopAsciinemaRecording_ActiveRecording_StopsAndFinalizesFile()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        var sessionId = await StartTerminalSessionAsync(client);
        var tempFile = GetTempFile();

        // Start recording
        var startResult = await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["filePath"] = tempFile
            },
            cancellationToken: TestCancellationToken);
        
        var startText = GetTextContent(startResult);
        Assert.IsNotNull(startText);
        var startResp = JsonSerializer.Deserialize<JsonElement>(startText);
        var recordingPath = startResp.GetProperty("filePath").GetString()!;

        // Wait a moment for some content to be recorded
        await Task.Delay(200);

        // Act
        var stopResult = await client.CallToolAsync(
            "stop_asciinema_recording",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);

        // Assert
        Assert.IsNotNull(stopResult);
        var stopText = GetTextContent(stopResult);
        Assert.IsNotNull(stopText);
        var stopResponse = JsonSerializer.Deserialize<JsonElement>(stopText);
        Assert.IsTrue(stopResponse.GetProperty("success").GetBoolean());
        Assert.IsTrue(stopResponse.GetProperty("wasRecording").GetBoolean());

        // Verify file contains valid asciinema content
        var content = await File.ReadAllTextAsync(recordingPath);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines.Length >= 1, "Should have at least a header");
        
        // Verify header is valid JSON with asciinema format
        var header = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.AreEqual(2, header.GetProperty("version").GetInt32());
        Assert.AreEqual(80, header.GetProperty("width").GetInt32());
        Assert.AreEqual(24, header.GetProperty("height").GetInt32());

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [TestMethod]
    public async Task ListTerminals_WithRecording_ShowsRecordingStatus()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        var sessionId = await StartTerminalSessionAsync(client);
        var tempFile = GetTempFile();

        // Start recording
        await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["filePath"] = tempFile
            },
            cancellationToken: TestCancellationToken);

        // Act - list terminals
        var listResult = await client.CallToolAsync(
            "list_terminals",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        // Assert
        var listText = GetTextContent(listResult);
        Assert.IsNotNull(listText);
        var listResponse = JsonSerializer.Deserialize<JsonElement>(listText);
        var sessions = listResponse.GetProperty("sessions");
        Assert.AreEqual(1, sessions.GetArrayLength());
        
        var session = sessions[0];
        Assert.IsTrue(session.GetProperty("isRecording").GetBoolean());
        Assert.IsNotNull(session.GetProperty("activeRecordingPath").GetString());

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [TestMethod]
    public async Task StartAsciinemaRecording_InvalidSessionId_ReturnsError()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var tempFile = GetTempFile();

        // Act
        var result = await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = "nonexistent-session-id",
                ["filePath"] = tempFile
            },
            cancellationToken: TestCancellationToken);

        // Assert
        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsFalse(response.GetProperty("success").GetBoolean());
        Assert.Contains("not found", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task StartAsciinemaRecording_AlreadyRecording_ReturnsError()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        var sessionId = await StartTerminalSessionAsync(client);
        var tempFile1 = GetTempFile();
        var tempFile2 = GetTempFile();

        // Start first recording
        await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["filePath"] = tempFile1
            },
            cancellationToken: TestCancellationToken);

        // Act - try to start second recording
        var result = await client.CallToolAsync(
            "start_asciinema_recording",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["filePath"] = tempFile2
            },
            cancellationToken: TestCancellationToken);

        // Assert
        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsFalse(response.GetProperty("success").GetBoolean());
        Assert.Contains("already recording", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [TestMethod]
    public async Task StopAsciinemaRecording_NotRecording_ReturnsGracefully()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        var sessionId = await StartTerminalSessionAsync(client);

        // Act - stop when not recording
        var result = await client.CallToolAsync(
            "stop_asciinema_recording",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);

        // Assert - this is considered success, just wasRecording is false
        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(response.GetProperty("success").GetBoolean());
        Assert.IsFalse(response.GetProperty("wasRecording").GetBoolean());
        Assert.Contains("not recording", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }
}
