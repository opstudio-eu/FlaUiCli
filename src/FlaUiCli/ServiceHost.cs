using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FlaUiCli.Core.Models;
using FlaUiCli.Core.Services;

namespace FlaUiCli;

/// <summary>
/// Hosts the automation service as a named pipe server.
/// This runs when the CLI is invoked with --service-mode.
/// </summary>
public class ServiceHost
{
    private readonly AutomationService _automationService = new();
    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastActivity = DateTime.Now;
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(5);

    public async Task RunAsync()
    {
        Console.WriteLine("FlaUI Service starting...");
        
        // Handle Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        // Start idle timeout checker
        _ = Task.Run(CheckIdleTimeoutAsync);

        try
        {
            await RunServerAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Service shutdown requested.");
        }
        finally
        {
            _automationService.Dispose();
            Console.WriteLine("FlaUI Service stopped.");
        }
    }

    private async Task CheckIdleTimeoutAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
                if (DateTime.Now - _lastActivity > _idleTimeout)
                {
                    Console.WriteLine("Idle timeout reached. Shutting down...");
                    _cts.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunServerAsync()
    {
        Console.WriteLine("Listening on pipe: flaui-service");
        
        while (!_cts.Token.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                "flaui-service",
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(_cts.Token);
                _lastActivity = DateTime.Now;
                
                await HandleClientAsync(server);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server)
    {
        try
        {
            // Read request
            var buffer = new byte[65536];
            var bytesRead = await server.ReadAsync(buffer, _cts.Token);
            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            var request = JsonSerializer.Deserialize<IpcRequest>(requestJson);
            if (request == null)
            {
                await SendResponseAsync(server, new IpcResponse 
                { 
                    Success = false, 
                    Error = new ErrorInfo { Code = "INVALID_REQUEST", Message = "Could not parse request" }
                });
                return;
            }

            _lastActivity = DateTime.Now;
            var response = ProcessCommand(request);
            await SendResponseAsync(server, response);
        }
        catch (Exception ex)
        {
            await SendResponseAsync(server, new IpcResponse
            {
                Success = false,
                Error = new ErrorInfo { Code = "INTERNAL_ERROR", Message = ex.Message }
            });
        }
    }

    private async Task SendResponseAsync(NamedPipeServerStream server, IpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(json);
        await server.WriteAsync(bytes, _cts.Token);
        await server.FlushAsync(_cts.Token);
    }

    private IpcResponse ProcessCommand(IpcRequest request)
    {
        try
        {
            object? result = request.Command.ToLowerInvariant() switch
            {
                "ping" => "pong",
                "status" => new
                {
                    connected = _automationService.IsConnected,
                    processId = _automationService.AttachedProcessId,
                    processName = _automationService.AttachedProcessName
                },
                "process.list" => _automationService.ListProcesses(),
                "connect" => Connect(request.Args),
                "disconnect" => Disconnect(),
                "window.list" => _automationService.GetWindows(),
                "window.main" => _automationService.GetMainWindow(),
                "window.focus" => FocusWindow(request.Args),
                "element.tree" => GetElementTree(request.Args),
                "element.find" => FindElements(request.Args),
                "element.info" => GetElementInfo(request.Args),
                "action.click" => Click(request.Args),
                "action.rightclick" => RightClick(request.Args),
                "action.doubleclick" => DoubleClick(request.Args),
                "action.type" => TypeText(request.Args),
                "action.clear" => ClearText(request.Args),
                "action.press" => PressKey(request.Args),
                "action.check" => SetCheck(request.Args, true),
                "action.uncheck" => SetCheck(request.Args, false),
                "action.toggle" => Toggle(request.Args),
                "action.select" => Select(request.Args),
                "action.expand" => Expand(request.Args),
                "action.collapse" => Collapse(request.Args),
                "action.invoke" => Invoke(request.Args),
                "get.text" => GetText(request.Args),
                "get.value" => GetValue(request.Args),
                "get.state" => GetState(request.Args),
                "get.patterns" => GetPatterns(request.Args),
                "wait.element" => WaitForElement(request.Args),
                "wait.gone" => WaitForGone(request.Args),
                "wait.enabled" => WaitForEnabled(request.Args),
                "screenshot" => TakeScreenshot(request.Args),
                "screenshot.base64" => TakeScreenshotBase64(request.Args),
                "shutdown" => Shutdown(),
                _ => throw new InvalidOperationException($"Unknown command: {request.Command}")
            };

            return new IpcResponse { Success = true, Data = result };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                Success = false,
                Error = new ErrorInfo { Code = "COMMAND_ERROR", Message = ex.Message }
            };
        }
    }

    private object? Connect(Dictionary<string, object?> args)
    {
        if (args.TryGetValue("pid", out var pidObj) && pidObj != null)
        {
            var pid = Convert.ToInt32(pidObj);
            _automationService.Connect(pid);
        }
        else if (args.TryGetValue("name", out var nameObj) && nameObj != null)
        {
            var name = nameObj is JsonElement je ? je.GetString()! : nameObj.ToString()!;
            _automationService.Connect(name);
        }
        else
        {
            throw new ArgumentException("Either 'pid' or 'name' must be provided");
        }
        
        return new
        {
            connected = true,
            processId = _automationService.AttachedProcessId,
            processName = _automationService.AttachedProcessName
        };
    }

    private object? Disconnect()
    {
        _automationService.Disconnect();
        return new { disconnected = true };
    }

    private object? FocusWindow(Dictionary<string, object?> args)
    {
        args.TryGetValue("id", out var id);
        _automationService.FocusWindow(id as string);
        return new { focused = true };
    }

    private object? GetElementTree(Dictionary<string, object?> args)
    {
        var depth = 5;
        if (args.TryGetValue("depth", out var d) && d != null)
        {
            depth = d is JsonElement je ? je.GetInt32() : Convert.ToInt32(d);
        }
        args.TryGetValue("rootId", out var rootId);
        var rootIdStr = rootId is JsonElement rje ? rje.GetString() : rootId as string;
        return _automationService.GetElementTree(depth, rootIdStr);
    }

    private object? FindElements(Dictionary<string, object?> args)
    {
        var criteria = new QueryCriteria
        {
            AutomationId = GetOptionalStringArg(args, "aid"),
            Name = GetOptionalStringArg(args, "name"),
            ControlType = GetOptionalStringArg(args, "type"),
            ClassName = GetOptionalStringArg(args, "class"),
            ParentId = GetOptionalStringArg(args, "parent"),
            FirstOnly = GetOptionalArg(args, "first", false)
        };
        return _automationService.FindElements(criteria);
    }

    private object? GetElementInfo(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        return _automationService.GetElement(id);
    }

    private object? Click(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Click(id);
        return new { clicked = true };
    }

    private object? RightClick(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Click(id, rightClick: true);
        return new { clicked = true };
    }

    private object? DoubleClick(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.DoubleClick(id);
        return new { clicked = true };
    }

    private object? TypeText(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        var text = GetRequiredArg<string>(args, "text");
        _automationService.TypeText(id, text);
        return new { typed = true };
    }

    private object? ClearText(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.ClearText(id);
        return new { cleared = true };
    }

    private object? PressKey(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        var key = GetRequiredArg<string>(args, "key");
        _automationService.PressKey(id, key);
        return new { pressed = true };
    }

    private object? SetCheck(Dictionary<string, object?> args, bool check)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.SetCheckState(id, check);
        return new { checked_ = check };
    }

    private object? Toggle(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Toggle(id);
        return new { toggled = true };
    }

    private object? Select(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        var item = GetRequiredArg<string>(args, "item");
        _automationService.Select(id, item);
        return new { selected = true };
    }

    private object? Expand(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Expand(id);
        return new { expanded = true };
    }

    private object? Collapse(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Collapse(id);
        return new { collapsed = true };
    }

    private object? Invoke(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        _automationService.Invoke(id);
        return new { invoked = true };
    }

    private object? GetText(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        return new { text = _automationService.GetText(id) };
    }

    private object? GetValue(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        return new { value = _automationService.GetValue(id) };
    }

    private object? GetState(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        return _automationService.GetState(id);
    }

    private object? GetPatterns(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        return new { patterns = _automationService.GetPatterns(id) };
    }

    private object? WaitForElement(Dictionary<string, object?> args)
    {
        var criteria = new QueryCriteria
        {
            AutomationId = GetOptionalStringArg(args, "aid"),
            Name = GetOptionalStringArg(args, "name"),
            ControlType = GetOptionalStringArg(args, "type"),
            ClassName = GetOptionalStringArg(args, "class"),
            FirstOnly = true
        };
        var timeout = GetOptionalArg(args, "timeout", 5000);
        
        var element = _automationService.WaitForElement(criteria, timeout);
        if (element == null)
        {
            throw new InvalidOperationException("Element not found within timeout");
        }
        return element;
    }

    private object? WaitForGone(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        var timeout = GetOptionalArg(args, "timeout", 5000);
        
        var gone = _automationService.WaitForElementGone(id, timeout);
        return new { gone };
    }

    private object? WaitForEnabled(Dictionary<string, object?> args)
    {
        var id = GetRequiredArg<string>(args, "id");
        var timeout = GetOptionalArg(args, "timeout", 5000);
        
        var enabled = _automationService.WaitForEnabled(id, timeout);
        return new { enabled };
    }

    private object? TakeScreenshot(Dictionary<string, object?> args)
    {
        var elementId = GetOptionalStringArg(args, "elementId");
        var output = GetOptionalStringArg(args, "output");
        
        var path = _automationService.TakeScreenshot(elementId, output);
        return new { path };
    }

    private object? TakeScreenshotBase64(Dictionary<string, object?> args)
    {
        var elementId = GetOptionalStringArg(args, "elementId");
        var base64 = _automationService.TakeScreenshotBase64(elementId);
        return new { base64 };
    }

    private object? Shutdown()
    {
        _cts.Cancel();
        return new { shutdown = true };
    }

    private static T GetRequiredArg<T>(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
        {
            throw new ArgumentException($"Required argument '{key}' not provided");
        }
        
        return ConvertArg<T>(value);
    }

    private static T GetOptionalArg<T>(Dictionary<string, object?> args, string key, T defaultValue)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }
        
        return ConvertArg<T>(value);
    }

    private static string? GetOptionalStringArg(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }
        
        return value is JsonElement je ? je.GetString() : value.ToString();
    }

    private static T ConvertArg<T>(object value)
    {
        if (value is JsonElement jsonElement)
        {
            if (typeof(T) == typeof(string))
                return (T)(object)jsonElement.GetString()!;
            if (typeof(T) == typeof(int))
                return (T)(object)jsonElement.GetInt32();
            if (typeof(T) == typeof(bool))
                return (T)(object)jsonElement.GetBoolean();
        }
        
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
