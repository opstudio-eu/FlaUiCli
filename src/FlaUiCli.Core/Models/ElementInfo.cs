using System.Text.Json.Serialization;

namespace FlaUiCli.Core.Models;

public class ElementInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("automationId")]
    public string AutomationId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("controlType")]
    public string ControlType { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("bounds")]
    public BoundsInfo? Bounds { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isOffscreen")]
    public bool IsOffscreen { get; set; }

    [JsonPropertyName("hasKeyboardFocus")]
    public bool HasKeyboardFocus { get; set; }

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    [JsonPropertyName("children")]
    public List<ElementInfo>? Children { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class BoundsInfo
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
