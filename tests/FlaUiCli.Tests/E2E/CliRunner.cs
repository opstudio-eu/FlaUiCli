using System.Diagnostics;
using System.Text.Json;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Helper class to execute FlaUiCli commands and parse JSON responses.
/// </summary>
public class CliRunner
{
    private readonly string _cliPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CliRunner(string cliPath)
    {
        _cliPath = cliPath;
    }

    /// <summary>
    /// Runs a CLI command and returns the raw JSON output.
    /// </summary>
    public async Task<string> RunRawAsync(string args, int timeoutMs = 30000)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"CLI command timed out after {timeoutMs}ms: {args}");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
        {
            throw new Exception($"CLI error (exit code {process.ExitCode}): {error}");
        }

        return output.Trim();
    }

    /// <summary>
    /// Runs a CLI command and returns the parsed JSON as a JsonDocument.
    /// </summary>
    public async Task<JsonDocument> RunAsync(string args, int timeoutMs = 30000)
    {
        var output = await RunRawAsync(args, timeoutMs);
        return JsonDocument.Parse(output);
    }

    /// <summary>
    /// Runs a CLI command and returns the parsed response.
    /// The CLI may output multiple JSON objects (e.g., service start message + actual response).
    /// We parse and return the last JSON object.
    /// </summary>
    public async Task<CliResponse> RunCommandAsync(string args, int timeoutMs = 30000)
    {
        var output = await RunRawAsync(args, timeoutMs);
        
        // The CLI may output multiple JSON objects when auto-starting the service
        // We need to parse the last complete JSON object
        var lastJson = ExtractLastJsonObject(output);
        
        return JsonSerializer.Deserialize<CliResponse>(lastJson, JsonOptions) 
            ?? throw new Exception($"Failed to parse CLI response: {output}");
    }
    
    /// <summary>
    /// Extracts the last complete JSON object from the output.
    /// The CLI may output multiple JSON objects separated by newlines.
    /// </summary>
    private static string ExtractLastJsonObject(string output)
    {
        // Find the last opening brace that starts a JSON object
        var lastBraceIndex = output.LastIndexOf("\n{", StringComparison.Ordinal);
        if (lastBraceIndex >= 0)
        {
            return output.Substring(lastBraceIndex + 1).Trim();
        }
        
        // If there's no newline before a brace, try to find if output starts with multiple objects
        // by looking for }{ pattern
        var objectBoundary = output.IndexOf("}\n{", StringComparison.Ordinal);
        if (objectBoundary >= 0)
        {
            return output.Substring(objectBoundary + 2).Trim();
        }
        
        // Single JSON object
        return output.Trim();
    }

    /// <summary>
    /// Runs a CLI command and asserts it succeeded.
    /// </summary>
    public async Task<JsonElement?> RunSuccessAsync(string args, int timeoutMs = 30000)
    {
        var response = await RunCommandAsync(args, timeoutMs);
        if (!response.Success)
        {
            throw new Exception($"CLI command failed: {response.ErrorMessage ?? "Unknown error"}");
        }
        return response.Data;
    }
}

/// <summary>
/// Represents a CLI response from FlaUiCli.
/// </summary>
public class CliResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public JsonElement? Data { get; set; }
    
    /// <summary>
    /// Error can be either a string or an object with Code/Message properties.
    /// Use ErrorMessage to get the error text regardless of format.
    /// </summary>
    public JsonElement? Error { get; set; }
    
    public bool? Running { get; set; }
    
    /// <summary>
    /// Gets the error message, handling both string and object error formats.
    /// </summary>
    public string? ErrorMessage
    {
        get
        {
            if (!Error.HasValue)
                return null;
                
            var error = Error.Value;
            
            // If error is a string
            if (error.ValueKind == JsonValueKind.String)
                return error.GetString();
                
            // If error is an object with Message property
            if (error.ValueKind == JsonValueKind.Object && 
                error.TryGetProperty("message", out var message))
                return message.GetString();
                
            // Fallback: return the raw JSON
            return error.ToString();
        }
    }
}
