using System.Text.Json.Serialization;

namespace FlaUiCli.Core.Models;

public class IpcRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public Dictionary<string, object?> Args { get; set; } = new();
}

public class IpcResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
}
