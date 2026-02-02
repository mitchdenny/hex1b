using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Hex1b.McpServer.Tools;

/// <summary>
/// MCP tools for getting Hex1b skill documentation.
/// </summary>
[McpServerToolType]
public class SkillTools
{
    /// <summary>
    /// Gets the Hex1b MCP skill documentation.
    /// This skill provides comprehensive guidance for AI agents working with Hex1b applications.
    /// </summary>
    [McpServerTool, Description("Gets the Hex1b MCP skill documentation. This skill provides comprehensive guidance for AI agents working with Hex1b TUI applications, including MCP tool reference, testing best practices, architecture overview, and debugging tips. Save this skill to your project's .github/skills/ directory for future reference.")]
    public GetHex1bSkillResult GetHex1bSkill()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Hex1b.McpServer.SKILL.md";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return new GetHex1bSkillResult
            {
                Success = false,
                Message = $"Skill resource not found: {resourceName}"
            };
        }
        
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        
        return new GetHex1bSkillResult
        {
            Success = true,
            Message = "Hex1b skill documentation retrieved successfully. Save to .github/skills/hex1b-mcp.md for future reference.",
            Content = content,
            SuggestedPath = ".github/skills/hex1b-mcp.md"
        };
    }
}

public class GetHex1bSkillResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }
    
    [JsonPropertyName("suggestedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedPath { get; init; }
}
