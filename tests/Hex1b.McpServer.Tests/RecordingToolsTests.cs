using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Hex1b.McpServer.Tests;

/// <summary>
/// Integration tests for the asciinema recording MCP tools.
/// </summary>
public class RecordingToolsTests : McpServerTestBase
{
    private readonly List<string> _tempFiles = new();

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

    [Fact]
    public async Task StartAsciinemaRecording_ValidSession_StartsRecording()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        // Create a terminal session first using the actual tool name
        var createResult = await client.CallToolAsync(
            "start_bash_terminal",
            new Dictionary<string, object?>
            {
                ["width"] = 80,
                ["height"] = 24,
                ["workingDirectory"] = "/tmp"
            },
            cancellationToken: TestCancellationToken);
        
        var createText = GetTextContent(createResult);
        Assert.NotNull(createText);
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var sessionId = createResponse.GetProperty("sessionId").GetString()!;
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
        Assert.NotNull(startResult);
        var startText = GetTextContent(startResult);
        Assert.NotNull(startText);
        var startResponse = JsonSerializer.Deserialize<JsonElement>(startText);
        Assert.True(startResponse.GetProperty("success").GetBoolean());
        // File path includes .cast extension enforcement
        Assert.Contains(".cast", startResponse.GetProperty("filePath").GetString());
        
        // Verify file was created
        var actualPath = startResponse.GetProperty("filePath").GetString()!;
        Assert.True(File.Exists(actualPath), "Recording file should exist");

        // Cleanup - remove the session
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [Fact]
    public async Task StopAsciinemaRecording_ActiveRecording_StopsAndFinalizesFile()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        // Create session
        var createResult = await client.CallToolAsync(
            "start_bash_terminal",
            new Dictionary<string, object?>
            {
                ["width"] = 80,
                ["height"] = 24
            },
            cancellationToken: TestCancellationToken);
        
        var createText = GetTextContent(createResult);
        Assert.NotNull(createText);
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var sessionId = createResponse.GetProperty("sessionId").GetString()!;
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
        Assert.NotNull(startText);
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
        Assert.NotNull(stopResult);
        var stopText = GetTextContent(stopResult);
        Assert.NotNull(stopText);
        var stopResponse = JsonSerializer.Deserialize<JsonElement>(stopText);
        Assert.True(stopResponse.GetProperty("success").GetBoolean());
        Assert.True(stopResponse.GetProperty("wasRecording").GetBoolean());

        // Verify file contains valid asciinema content
        var content = await File.ReadAllTextAsync(recordingPath);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 1, "Should have at least a header");
        
        // Verify header is valid JSON with asciinema format
        var header = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.Equal(2, header.GetProperty("version").GetInt32());
        Assert.Equal(80, header.GetProperty("width").GetInt32());
        Assert.Equal(24, header.GetProperty("height").GetInt32());

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [Fact]
    public async Task ListTerminals_WithRecording_ShowsRecordingStatus()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        // Create session
        var createResult = await client.CallToolAsync(
            "start_bash_terminal",
            new Dictionary<string, object?>
            {
                ["width"] = 80,
                ["height"] = 24
            },
            cancellationToken: TestCancellationToken);
        
        var createText = GetTextContent(createResult);
        Assert.NotNull(createText);
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var sessionId = createResponse.GetProperty("sessionId").GetString()!;
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
        Assert.NotNull(listText);
        var listResponse = JsonSerializer.Deserialize<JsonElement>(listText);
        var sessions = listResponse.GetProperty("sessions");
        Assert.Equal(1, sessions.GetArrayLength());
        
        var session = sessions[0];
        Assert.True(session.GetProperty("isRecording").GetBoolean());
        Assert.NotNull(session.GetProperty("activeRecordingPath").GetString());

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [Fact]
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
        Assert.NotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Contains("not found", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsciinemaRecording_AlreadyRecording_ReturnsError()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        // Create session
        var createResult = await client.CallToolAsync(
            "start_bash_terminal",
            new Dictionary<string, object?>
            {
                ["width"] = 80,
                ["height"] = 24
            },
            cancellationToken: TestCancellationToken);
        
        var createText = GetTextContent(createResult);
        Assert.NotNull(createText);
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var sessionId = createResponse.GetProperty("sessionId").GetString()!;
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
        Assert.NotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Contains("already recording", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }

    [Fact]
    public async Task StopAsciinemaRecording_NotRecording_ReturnsGracefully()
    {
        // Arrange
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        
        // Create session without starting recording
        var createResult = await client.CallToolAsync(
            "start_bash_terminal",
            new Dictionary<string, object?>
            {
                ["width"] = 80,
                ["height"] = 24
            },
            cancellationToken: TestCancellationToken);
        
        var createText = GetTextContent(createResult);
        Assert.NotNull(createText);
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createText);
        var sessionId = createResponse.GetProperty("sessionId").GetString()!;

        // Act - stop when not recording
        var result = await client.CallToolAsync(
            "stop_asciinema_recording",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);

        // Assert - this is considered success, just wasRecording is false
        var text = GetTextContent(result);
        Assert.NotNull(text);
        var response = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(response.GetProperty("success").GetBoolean());
        Assert.False(response.GetProperty("wasRecording").GetBoolean());
        Assert.Contains("not recording", response.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        // Cleanup
        await client.CallToolAsync(
            "remove_session",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken: TestCancellationToken);
    }
}
