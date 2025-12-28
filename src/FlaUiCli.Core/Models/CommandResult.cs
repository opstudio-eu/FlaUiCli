using System.Text.Json.Serialization;

namespace FlaUiCli.Core.Models;

public class CommandResult<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    public static CommandResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static CommandResult<T> Fail(string code, string message) => new()
    {
        Success = false,
        Error = new ErrorInfo { Code = code, Message = message }
    };
}

public class CommandResult : CommandResult<object?>
{
    public static CommandResult Ok() => new() { Success = true };
    
    public new static CommandResult Fail(string code, string message) => new()
    {
        Success = false,
        Error = new ErrorInfo { Code = code, Message = message }
    };
}

public class ErrorInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
