using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Hex1b.Tool.Tests;

public class RecordingProtocolTests
{
    [Fact]
    public async Task RecordStart_StartsRecordingAndCreatesFile()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithHeadless()
            .WithHex1bApp((app, options) => ctx => new Hex1b.Widgets.TextBlockWidget("Hello"))
            .WithMcpDiagnostics(appName: "RecordTest", forceEnable: true)
            .Build();

        _ = terminal.RunAsync();

        var socketPath = Hex1b.Diagnostics.McpDiagnosticsPresentationFilter.GetSocketPath();
        await WaitForSocketAsync(socketPath);

        var outputPath = Path.Combine(Path.GetTempPath(), $"hex1b-test-{Guid.NewGuid():N}.cast");
        try
        {
            // Start recording
            var startResponse = await SendRawRequestAsync(socketPath,
                $$$"""{"method":"record-start","filePath":"{{{outputPath.Replace("\\", "\\\\")}}}","title":"Test Recording"}""");

            Assert.True(startResponse.GetProperty("success").GetBoolean(), GetError(startResponse));
            Assert.True(startResponse.GetProperty("recording").GetBoolean());
            Assert.Equal(outputPath, startResponse.GetProperty("recordingPath").GetString());

            // Check status
            var statusResponse = await SendRawRequestAsync(socketPath, """{"method":"record-status"}""");
            Assert.True(statusResponse.GetProperty("success").GetBoolean());
            Assert.True(statusResponse.GetProperty("recording").GetBoolean());

            // Info should also reflect recording status
            var infoResponse = await SendRawRequestAsync(socketPath, """{"method":"info"}""");
            Assert.True(infoResponse.GetProperty("success").GetBoolean());
            Assert.True(infoResponse.GetProperty("recording").GetBoolean());

            // Stop recording
            var stopResponse = await SendRawRequestAsync(socketPath, """{"method":"record-stop"}""");
            Assert.True(stopResponse.GetProperty("success").GetBoolean(), GetError(stopResponse));
            Assert.False(stopResponse.GetProperty("recording").GetBoolean());
            Assert.Equal(outputPath, stopResponse.GetProperty("recordingPath").GetString());

            // File should exist and be valid asciicast v2
            Assert.True(File.Exists(outputPath));
            var lines = await File.ReadAllLinesAsync(outputPath);
            Assert.True(lines.Length >= 1);

            using var header = JsonDocument.Parse(lines[0]);
            Assert.Equal(2, header.RootElement.GetProperty("version").GetInt32());
            Assert.Equal(80, header.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(24, header.RootElement.GetProperty("height").GetInt32());
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task RecordStart_WhenAlreadyRecording_ReturnsError()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithHeadless()
            .WithHex1bApp((app, options) => ctx => new Hex1b.Widgets.TextBlockWidget("Hello"))
            .WithMcpDiagnostics(appName: "RecordTest2", forceEnable: true)
            .Build();

        _ = terminal.RunAsync();

        var socketPath = Hex1b.Diagnostics.McpDiagnosticsPresentationFilter.GetSocketPath();
        await WaitForSocketAsync(socketPath);

        var outputPath1 = Path.Combine(Path.GetTempPath(), $"hex1b-test-{Guid.NewGuid():N}.cast");
        var outputPath2 = Path.Combine(Path.GetTempPath(), $"hex1b-test-{Guid.NewGuid():N}.cast");
        try
        {
            var response1 = await SendRawRequestAsync(socketPath,
                $$$"""{"method":"record-start","filePath":"{{{outputPath1.Replace("\\", "\\\\")}}}"}""");
            Assert.True(response1.GetProperty("success").GetBoolean());

            var response2 = await SendRawRequestAsync(socketPath,
                $$$"""{"method":"record-start","filePath":"{{{outputPath2.Replace("\\", "\\\\")}}}"}""");
            Assert.False(response2.GetProperty("success").GetBoolean());
            Assert.Contains("Already recording", response2.GetProperty("error").GetString());

            await SendRawRequestAsync(socketPath, """{"method":"record-stop"}""");
        }
        finally
        {
            if (File.Exists(outputPath1)) File.Delete(outputPath1);
            if (File.Exists(outputPath2)) File.Delete(outputPath2);
        }
    }

    [Fact]
    public async Task RecordStop_WhenNotRecording_ReturnsError()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithHeadless()
            .WithHex1bApp((app, options) => ctx => new Hex1b.Widgets.TextBlockWidget("Hello"))
            .WithMcpDiagnostics(appName: "RecordTest3", forceEnable: true)
            .Build();

        _ = terminal.RunAsync();

        var socketPath = Hex1b.Diagnostics.McpDiagnosticsPresentationFilter.GetSocketPath();
        await WaitForSocketAsync(socketPath);

        var response = await SendRawRequestAsync(socketPath, """{"method":"record-stop"}""");
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Contains("Not currently recording", response.GetProperty("error").GetString());
    }

    private static string GetError(JsonElement response)
    {
        return response.TryGetProperty("error", out var err) ? err.GetString() ?? "" : "";
    }

    private static async Task<JsonElement> SendRawRequestAsync(string socketPath, string requestJson)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await writer.WriteLineAsync(requestJson);

        var responseLine = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(responseLine!);
        return doc.RootElement.Clone();
    }

    private static async Task WaitForSocketAsync(string socketPath, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(socketPath))
            {
                await Task.Delay(50);
                return;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"Socket {socketPath} did not appear within {timeoutMs}ms");
    }
}
