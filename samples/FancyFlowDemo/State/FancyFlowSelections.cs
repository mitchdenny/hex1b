namespace FancyFlowDemo.State;

/// <summary>
/// Holds the answers collected across the FancyFlowDemo prompt sequence.
/// Mutated by each prompt's completion handler and read after the flow finishes
/// to print the summary block.
/// </summary>
internal sealed class FancyFlowSelections
{
    public string Language { get; set; } = "C#";
    public string TemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string Folder { get; set; } = "./my-app";
    public string HostnamePattern { get; set; } = "localhost";
}
