using System.Diagnostics;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Shared test fixture that manages the TestApp process and CLI runner.
/// This fixture is shared across all E2E tests for performance.
/// </summary>
public class TestFixture : IAsyncLifetime
{
    private Process? _testAppProcess;
    private readonly string _solutionRoot;
    
    public CliRunner Cli { get; private set; } = null!;
    public int TestAppPid { get; private set; }
    public string TestAppPath { get; private set; } = string.Empty;
    public string CliPath { get; private set; } = string.Empty;

    public TestFixture()
    {
        // Find solution root by walking up from test assembly location
        var currentDir = AppContext.BaseDirectory;
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "FlaUiCli.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        _solutionRoot = currentDir ?? throw new Exception("Could not find solution root");
    }

    public async Task InitializeAsync()
    {
        // Build the CLI and TestApp if needed
        await BuildProjectsAsync();

        // Start the test app
        await StartTestAppAsync();

        // Wait for the window to appear
        await Task.Delay(1500);

        // Create CLI runner
        Cli = new CliRunner(CliPath);

        // Connect to the test app
        var connectResult = await Cli.RunCommandAsync($"connect --pid {TestAppPid}");
        if (!connectResult.Success)
        {
            throw new Exception($"Failed to connect to TestApp: {connectResult.ErrorMessage}");
        }
    }

    public async Task DisposeAsync()
    {
        // Disconnect and stop service
        try
        {
            await Cli.RunRawAsync("disconnect", timeoutMs: 5000);
        }
        catch { /* Ignore errors during cleanup */ }

        try
        {
            await Cli.RunRawAsync("service stop", timeoutMs: 5000);
        }
        catch { /* Ignore errors during cleanup */ }

        // Kill the test app
        if (_testAppProcess != null && !_testAppProcess.HasExited)
        {
            _testAppProcess.Kill(entireProcessTree: true);
            _testAppProcess.Dispose();
        }
    }

    private async Task BuildProjectsAsync()
    {
        // Determine paths
        var configuration = "Debug";
        
        CliPath = Path.Combine(_solutionRoot, "src", "FlaUiCli", "bin", configuration, "net8.0-windows", "flaui.exe");
        TestAppPath = Path.Combine(_solutionRoot, "src", "FlaUiCli.TestApp", "bin", configuration, "net8.0-windows", "FlaUiTestApp.exe");

        // Check if we need to build
        if (!File.Exists(CliPath) || !File.Exists(TestAppPath))
        {
            // Build the solution
            var buildProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{Path.Combine(_solutionRoot, "FlaUiCli.sln")}\" -c {configuration}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            buildProcess.Start();
            var output = await buildProcess.StandardOutput.ReadToEndAsync();
            var error = await buildProcess.StandardError.ReadToEndAsync();
            await buildProcess.WaitForExitAsync();

            if (buildProcess.ExitCode != 0)
            {
                throw new Exception($"Build failed: {error}\n{output}");
            }
        }

        if (!File.Exists(CliPath))
        {
            throw new FileNotFoundException($"CLI not found at: {CliPath}");
        }

        if (!File.Exists(TestAppPath))
        {
            throw new FileNotFoundException($"TestApp not found at: {TestAppPath}");
        }
    }

    private async Task StartTestAppAsync()
    {
        _testAppProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = TestAppPath,
                UseShellExecute = true,
                CreateNoWindow = false
            }
        };

        _testAppProcess.Start();
        TestAppPid = _testAppProcess.Id;

        // Wait for the process to be ready
        await Task.Delay(500);

        if (_testAppProcess.HasExited)
        {
            throw new Exception("TestApp exited immediately after starting");
        }
    }

    /// <summary>
    /// Restart the TestApp (for tests that need a fresh app state).
    /// </summary>
    public async Task RestartTestAppAsync()
    {
        // Kill current instance
        if (_testAppProcess != null && !_testAppProcess.HasExited)
        {
            _testAppProcess.Kill(entireProcessTree: true);
            _testAppProcess.Dispose();
        }

        // Disconnect from old process
        try
        {
            await Cli.RunRawAsync("disconnect", timeoutMs: 5000);
        }
        catch { /* Ignore */ }

        // Start new instance
        await StartTestAppAsync();
        await Task.Delay(1500);

        // Reconnect
        var connectResult = await Cli.RunCommandAsync($"connect --pid {TestAppPid}");
        if (!connectResult.Success)
        {
            throw new Exception($"Failed to reconnect to TestApp: {connectResult.ErrorMessage}");
        }
    }
}

/// <summary>
/// xUnit collection definition for E2E tests.
/// All tests in this collection share the same TestFixture instance.
/// </summary>
[CollectionDefinition("E2E")]
public class E2ETestCollection : ICollectionFixture<TestFixture>
{
}
