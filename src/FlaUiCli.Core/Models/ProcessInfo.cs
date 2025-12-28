using System.Text.Json.Serialization;

namespace FlaUiCli.Core.Models;

public class ProcessInfo
{
    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mainWindowTitle")]
    public string MainWindowTitle { get; set; } = string.Empty;

    [JsonPropertyName("hasWindow")]
    public bool HasWindow { get; set; }
}
