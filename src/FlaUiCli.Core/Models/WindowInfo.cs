using System.Text.Json.Serialization;

namespace FlaUiCli.Core.Models;

public class WindowInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("automationId")]
    public string AutomationId { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("bounds")]
    public BoundsInfo? Bounds { get; set; }

    [JsonPropertyName("isModal")]
    public bool IsModal { get; set; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }
}
