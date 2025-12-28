using System.CommandLine;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FlaUiCli.Core.Models;

namespace FlaUiCli;

public class Program
{
    private const string PipeName = "flaui-service";
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> Main(string[] args)
    {
        // Check for service mode first (before System.CommandLine parsing)
        if (args.Length > 0 && args[0] == "--service-mode")
        {
            var serviceHost = new ServiceHost();
            await serviceHost.RunAsync();
            return 0;
        }

        var rootCommand = new RootCommand("FlaUI CLI - Windows UI Automation Tool for AI Agents");

        // Service commands
        var serviceCommand = new Command("service", "Manage the FlaUI background service");
        var serviceStartCommand = new Command("start", "Start the FlaUI service");
        var serviceStopCommand = new Command("stop", "Stop the FlaUI service");
        var serviceStatusCommand = new Command("status", "Check the FlaUI service status");
        
        serviceStartCommand.SetHandler(StartService);
        serviceStopCommand.SetHandler(StopService);
        serviceStatusCommand.SetHandler(ServiceStatus);
        
        serviceCommand.AddCommand(serviceStartCommand);
        serviceCommand.AddCommand(serviceStopCommand);
        serviceCommand.AddCommand(serviceStatusCommand);
        rootCommand.AddCommand(serviceCommand);

        // Process commands
        var processCommand = new Command("process", "Process-related commands");
        var processListCommand = new Command("list", "List processes with windows");
        processListCommand.SetHandler(async () => await SendAndPrint("process.list"));
        processCommand.AddCommand(processListCommand);
        rootCommand.AddCommand(processCommand);

        // Connect/Disconnect
        var connectCommand = new Command("connect", "Connect to a running application");
        var connectPidOption = new Option<int?>("--pid", "Process ID");
        var connectNameOption = new Option<string?>("--name", "Process name");
        connectCommand.AddOption(connectPidOption);
        connectCommand.AddOption(connectNameOption);
        connectCommand.SetHandler(async (pid, name) =>
        {
            var cmdArgs = new Dictionary<string, object?>();
            if (pid.HasValue) cmdArgs["pid"] = pid.Value;
            if (!string.IsNullOrEmpty(name)) cmdArgs["name"] = name;
            await SendAndPrint("connect", cmdArgs);
        }, connectPidOption, connectNameOption);
        rootCommand.AddCommand(connectCommand);

        var disconnectCommand = new Command("disconnect", "Disconnect from the current application");
        disconnectCommand.SetHandler(async () => await SendAndPrint("disconnect"));
        rootCommand.AddCommand(disconnectCommand);

        var statusCommand = new Command("status", "Show current connection status");
        statusCommand.SetHandler(async () => await SendAndPrint("status"));
        rootCommand.AddCommand(statusCommand);

        // Window commands
        var windowCommand = new Command("window", "Window-related commands");
        
        var windowListCommand = new Command("list", "List all windows");
        windowListCommand.SetHandler(async () => await SendAndPrint("window.list"));
        windowCommand.AddCommand(windowListCommand);

        var windowFocusCommand = new Command("focus", "Focus a window");
        var windowIdOption = new Option<string?>("--id", "Window ID");
        windowFocusCommand.AddOption(windowIdOption);
        windowFocusCommand.SetHandler(async (id) =>
        {
            var cmdArgs = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(id)) cmdArgs["id"] = id;
            await SendAndPrint("window.focus", cmdArgs);
        }, windowIdOption);
        windowCommand.AddCommand(windowFocusCommand);
        
        rootCommand.AddCommand(windowCommand);

        // Element commands
        var elementCommand = new Command("element", "Element-related commands");
        
        var elementTreeCommand = new Command("tree", "Get element tree");
        var depthOption = new Option<int>("--depth", () => 3, "Tree depth");
        var rootIdOption = new Option<string?>("--root", "Root element ID");
        elementTreeCommand.AddOption(depthOption);
        elementTreeCommand.AddOption(rootIdOption);
        elementTreeCommand.SetHandler(async (depth, rootId) =>
        {
            var cmdArgs = new Dictionary<string, object?> { ["depth"] = depth };
            if (!string.IsNullOrEmpty(rootId)) cmdArgs["rootId"] = rootId;
            await SendAndPrint("element.tree", cmdArgs);
        }, depthOption, rootIdOption);
        elementCommand.AddCommand(elementTreeCommand);

        var elementFindCommand = new Command("find", "Find elements");
        var aidOption = new Option<string?>("--aid", "Automation ID");
        var nameOption = new Option<string?>("--name", "Element name");
        var typeOption = new Option<string?>("--type", "Control type");
        var classOption = new Option<string?>("--class", "Class name");
        var parentOption = new Option<string?>("--parent", "Parent element ID");
        var firstOption = new Option<bool>("--first", "Return first match only");
        elementFindCommand.AddOption(aidOption);
        elementFindCommand.AddOption(nameOption);
        elementFindCommand.AddOption(typeOption);
        elementFindCommand.AddOption(classOption);
        elementFindCommand.AddOption(parentOption);
        elementFindCommand.AddOption(firstOption);
        elementFindCommand.SetHandler(async (aid, name, type, cls, parent, first) =>
        {
            var cmdArgs = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(aid)) cmdArgs["aid"] = aid;
            if (!string.IsNullOrEmpty(name)) cmdArgs["name"] = name;
            if (!string.IsNullOrEmpty(type)) cmdArgs["type"] = type;
            if (!string.IsNullOrEmpty(cls)) cmdArgs["class"] = cls;
            if (!string.IsNullOrEmpty(parent)) cmdArgs["parent"] = parent;
            if (first) cmdArgs["first"] = true;
            await SendAndPrint("element.find", cmdArgs);
        }, aidOption, nameOption, typeOption, classOption, parentOption, firstOption);
        elementCommand.AddCommand(elementFindCommand);

        var elementInfoCommand = new Command("info", "Get element information");
        var elementIdArg = new Argument<string>("id", "Element ID");
        elementInfoCommand.AddArgument(elementIdArg);
        elementInfoCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("element.info", new Dictionary<string, object?> { ["id"] = id });
        }, elementIdArg);
        elementCommand.AddCommand(elementInfoCommand);

        rootCommand.AddCommand(elementCommand);

        // Action commands
        var actionCommand = new Command("action", "Perform actions on elements");

        var clickCommand = new Command("click", "Click an element");
        var clickIdArg = new Argument<string>("id", "Element ID");
        clickCommand.AddArgument(clickIdArg);
        clickCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.click", new Dictionary<string, object?> { ["id"] = id });
        }, clickIdArg);
        actionCommand.AddCommand(clickCommand);

        var rightClickCommand = new Command("rightclick", "Right-click an element");
        var rightClickIdArg = new Argument<string>("id", "Element ID");
        rightClickCommand.AddArgument(rightClickIdArg);
        rightClickCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.rightclick", new Dictionary<string, object?> { ["id"] = id });
        }, rightClickIdArg);
        actionCommand.AddCommand(rightClickCommand);

        var doubleClickCommand = new Command("doubleclick", "Double-click an element");
        var doubleClickIdArg = new Argument<string>("id", "Element ID");
        doubleClickCommand.AddArgument(doubleClickIdArg);
        doubleClickCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.doubleclick", new Dictionary<string, object?> { ["id"] = id });
        }, doubleClickIdArg);
        actionCommand.AddCommand(doubleClickCommand);

        var typeCommand = new Command("type", "Type text into an element");
        var typeIdArg = new Argument<string>("id", "Element ID");
        var typeTextArg = new Argument<string>("text", "Text to type");
        typeCommand.AddArgument(typeIdArg);
        typeCommand.AddArgument(typeTextArg);
        typeCommand.SetHandler(async (id, text) =>
        {
            await SendAndPrint("action.type", new Dictionary<string, object?> { ["id"] = id, ["text"] = text });
        }, typeIdArg, typeTextArg);
        actionCommand.AddCommand(typeCommand);

        var clearCommand = new Command("clear", "Clear text from an element");
        var clearIdArg = new Argument<string>("id", "Element ID");
        clearCommand.AddArgument(clearIdArg);
        clearCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.clear", new Dictionary<string, object?> { ["id"] = id });
        }, clearIdArg);
        actionCommand.AddCommand(clearCommand);

        var checkCommand = new Command("check", "Check a checkbox");
        var checkIdArg = new Argument<string>("id", "Element ID");
        checkCommand.AddArgument(checkIdArg);
        checkCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.check", new Dictionary<string, object?> { ["id"] = id });
        }, checkIdArg);
        actionCommand.AddCommand(checkCommand);

        var uncheckCommand = new Command("uncheck", "Uncheck a checkbox");
        var uncheckIdArg = new Argument<string>("id", "Element ID");
        uncheckCommand.AddArgument(uncheckIdArg);
        uncheckCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.uncheck", new Dictionary<string, object?> { ["id"] = id });
        }, uncheckIdArg);
        actionCommand.AddCommand(uncheckCommand);

        var toggleCommand = new Command("toggle", "Toggle element state");
        var toggleIdArg = new Argument<string>("id", "Element ID");
        toggleCommand.AddArgument(toggleIdArg);
        toggleCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.toggle", new Dictionary<string, object?> { ["id"] = id });
        }, toggleIdArg);
        actionCommand.AddCommand(toggleCommand);

        var selectCommand = new Command("select", "Select an item in a list/combo");
        var selectIdArg = new Argument<string>("id", "Element ID");
        var selectItemArg = new Argument<string>("item", "Item text to select");
        selectCommand.AddArgument(selectIdArg);
        selectCommand.AddArgument(selectItemArg);
        selectCommand.SetHandler(async (id, item) =>
        {
            await SendAndPrint("action.select", new Dictionary<string, object?> { ["id"] = id, ["item"] = item });
        }, selectIdArg, selectItemArg);
        actionCommand.AddCommand(selectCommand);

        var expandCommand = new Command("expand", "Expand an element");
        var expandIdArg = new Argument<string>("id", "Element ID");
        expandCommand.AddArgument(expandIdArg);
        expandCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.expand", new Dictionary<string, object?> { ["id"] = id });
        }, expandIdArg);
        actionCommand.AddCommand(expandCommand);

        var collapseCommand = new Command("collapse", "Collapse an element");
        var collapseIdArg = new Argument<string>("id", "Element ID");
        collapseCommand.AddArgument(collapseIdArg);
        collapseCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.collapse", new Dictionary<string, object?> { ["id"] = id });
        }, collapseIdArg);
        actionCommand.AddCommand(collapseCommand);

        var invokeCommand = new Command("invoke", "Invoke default action");
        var invokeIdArg = new Argument<string>("id", "Element ID");
        invokeCommand.AddArgument(invokeIdArg);
        invokeCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("action.invoke", new Dictionary<string, object?> { ["id"] = id });
        }, invokeIdArg);
        actionCommand.AddCommand(invokeCommand);

        rootCommand.AddCommand(actionCommand);

        // Get commands
        var getCommand = new Command("get", "Get element properties");

        var getTextCommand = new Command("text", "Get text content");
        var getTextIdArg = new Argument<string>("id", "Element ID");
        getTextCommand.AddArgument(getTextIdArg);
        getTextCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("get.text", new Dictionary<string, object?> { ["id"] = id });
        }, getTextIdArg);
        getCommand.AddCommand(getTextCommand);

        var getValueCommand = new Command("value", "Get element value");
        var getValueIdArg = new Argument<string>("id", "Element ID");
        getValueCommand.AddArgument(getValueIdArg);
        getValueCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("get.value", new Dictionary<string, object?> { ["id"] = id });
        }, getValueIdArg);
        getCommand.AddCommand(getValueCommand);

        var getStateCommand = new Command("state", "Get element state");
        var getStateIdArg = new Argument<string>("id", "Element ID");
        getStateCommand.AddArgument(getStateIdArg);
        getStateCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("get.state", new Dictionary<string, object?> { ["id"] = id });
        }, getStateIdArg);
        getCommand.AddCommand(getStateCommand);

        var getPatternsCommand = new Command("patterns", "Get supported patterns");
        var getPatternsIdArg = new Argument<string>("id", "Element ID");
        getPatternsCommand.AddArgument(getPatternsIdArg);
        getPatternsCommand.SetHandler(async (id) =>
        {
            await SendAndPrint("get.patterns", new Dictionary<string, object?> { ["id"] = id });
        }, getPatternsIdArg);
        getCommand.AddCommand(getPatternsCommand);

        rootCommand.AddCommand(getCommand);

        // Wait commands
        var waitCommand = new Command("wait", "Wait for conditions");

        var waitElementCommand = new Command("element", "Wait for element to appear");
        waitElementCommand.AddOption(aidOption);
        waitElementCommand.AddOption(nameOption);
        waitElementCommand.AddOption(typeOption);
        var timeoutOption = new Option<int>("--timeout", () => 5000, "Timeout in milliseconds");
        waitElementCommand.AddOption(timeoutOption);
        waitElementCommand.SetHandler(async (aid, name, type, timeout) =>
        {
            var cmdArgs = new Dictionary<string, object?> { ["timeout"] = timeout };
            if (!string.IsNullOrEmpty(aid)) cmdArgs["aid"] = aid;
            if (!string.IsNullOrEmpty(name)) cmdArgs["name"] = name;
            if (!string.IsNullOrEmpty(type)) cmdArgs["type"] = type;
            await SendAndPrint("wait.element", cmdArgs);
        }, aidOption, nameOption, typeOption, timeoutOption);
        waitCommand.AddCommand(waitElementCommand);

        var waitGoneCommand = new Command("gone", "Wait for element to disappear");
        var waitGoneIdArg = new Argument<string>("id", "Element ID");
        waitGoneCommand.AddArgument(waitGoneIdArg);
        waitGoneCommand.AddOption(timeoutOption);
        waitGoneCommand.SetHandler(async (id, timeout) =>
        {
            await SendAndPrint("wait.gone", new Dictionary<string, object?> { ["id"] = id, ["timeout"] = timeout });
        }, waitGoneIdArg, timeoutOption);
        waitCommand.AddCommand(waitGoneCommand);

        var waitEnabledCommand = new Command("enabled", "Wait for element to be enabled");
        var waitEnabledIdArg = new Argument<string>("id", "Element ID");
        waitEnabledCommand.AddArgument(waitEnabledIdArg);
        waitEnabledCommand.AddOption(timeoutOption);
        waitEnabledCommand.SetHandler(async (id, timeout) =>
        {
            await SendAndPrint("wait.enabled", new Dictionary<string, object?> { ["id"] = id, ["timeout"] = timeout });
        }, waitEnabledIdArg, timeoutOption);
        waitCommand.AddCommand(waitEnabledCommand);

        rootCommand.AddCommand(waitCommand);

        // Screenshot command
        var screenshotCommand = new Command("screenshot", "Take a screenshot");
        var screenshotElementOption = new Option<string?>("--element", "Element ID (optional, screenshots window if not provided)");
        var screenshotOutputOption = new Option<string?>("--output", "Output file path");
        var screenshotBase64Option = new Option<bool>("--base64", "Return as base64 instead of saving to file");
        screenshotCommand.AddOption(screenshotElementOption);
        screenshotCommand.AddOption(screenshotOutputOption);
        screenshotCommand.AddOption(screenshotBase64Option);
        screenshotCommand.SetHandler(async (element, output, base64) =>
        {
            var cmdArgs = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(element)) cmdArgs["elementId"] = element;
            if (!string.IsNullOrEmpty(output)) cmdArgs["output"] = output;
            
            var command = base64 ? "screenshot.base64" : "screenshot";
            await SendAndPrint(command, cmdArgs);
        }, screenshotElementOption, screenshotOutputOption, screenshotBase64Option);
        rootCommand.AddCommand(screenshotCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task StartService()
    {
        // Check if already running
        if (await IsServiceRunning())
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, message = "Service is already running" }, JsonOptions));
            return;
        }

        // Get the current executable path - this same executable will run in service mode
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        
        if (string.IsNullOrEmpty(exePath))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Could not determine executable path" }, JsonOptions));
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--service-mode",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        try
        {
            Process.Start(psi);
            
            // Wait for service to start
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(200);
                if (await IsServiceRunning())
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = true, message = "Service started successfully" }, JsonOptions));
                    return;
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Service started but not responding" }, JsonOptions));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions));
        }
    }

    private static async Task StopService()
    {
        if (!await IsServiceRunning())
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, message = "Service is not running" }, JsonOptions));
            return;
        }

        try
        {
            await SendCommand("shutdown", new Dictionary<string, object?>());
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, message = "Service stopped" }, JsonOptions));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions));
        }
    }

    private static async Task ServiceStatus()
    {
        var running = await IsServiceRunning();
        if (running)
        {
            try
            {
                var response = await SendCommand("status", new Dictionary<string, object?>());
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, running = true, data = response?.Data }, JsonOptions));
            }
            catch
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, running = true }, JsonOptions));
            }
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, running = false }, JsonOptions));
        }
    }

    private static async Task<bool> IsServiceRunning()
    {
        try
        {
            var response = await SendCommand("ping", new Dictionary<string, object?>(), timeout: 1000);
            return response?.Success == true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task SendAndPrint(string command, Dictionary<string, object?>? args = null)
    {
        // Auto-start service if not running
        if (!await IsServiceRunning())
        {
            await StartService();
            await Task.Delay(500);
        }

        try
        {
            var response = await SendCommand(command, args ?? new Dictionary<string, object?>());
            Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new IpcResponse
            {
                Success = false,
                Error = new ErrorInfo { Code = "CONNECTION_ERROR", Message = ex.Message }
            }, JsonOptions));
        }
    }

    private static async Task<IpcResponse?> SendCommand(string command, Dictionary<string, object?> args, int timeout = 30000)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        
        var cts = new CancellationTokenSource(timeout);
        await client.ConnectAsync(cts.Token);

        var request = new IpcRequest { Command = command, Args = args };
        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        
        await client.WriteAsync(requestBytes, cts.Token);
        await client.FlushAsync(cts.Token);

        var buffer = new byte[1024 * 1024]; // 1MB buffer
        var bytesRead = await client.ReadAsync(buffer, cts.Token);
        var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        return JsonSerializer.Deserialize<IpcResponse>(responseJson);
    }
}
