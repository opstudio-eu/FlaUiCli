using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for service management commands (service start, stop, status).
/// </summary>
[Collection("E2E")]
public class ServiceTests
{
    private readonly TestFixture _fixture;
    private CliRunner Cli => _fixture.Cli;

    public ServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ServiceStatus_WhenRunning_ReturnsRunningTrue()
    {
        // The service should already be running from the fixture
        var result = await Cli.RunCommandAsync("service status");

        result.Success.Should().BeTrue();
        result.Running.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceStart_WhenAlreadyRunning_ReturnsAlreadyRunning()
    {
        // The service should already be running from the fixture
        var result = await Cli.RunCommandAsync("service start");

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("already running");
    }

    [Fact]
    public async Task ServiceStop_ThenStart_Works()
    {
        try
        {
            // Stop the service - this may return an error because the pipe closes
            // when the service stops, but the service should still stop
            var stopResult = await Cli.RunCommandAsync("service stop");
            // Service stop may fail to return clean JSON because the pipe closes
            // We check the actual status instead

            // Give it a moment to fully stop
            await Task.Delay(500);

            // Verify it's stopped
            var statusResult = await Cli.RunCommandAsync("service status");
            statusResult.Success.Should().BeTrue();
            statusResult.Running.Should().BeFalse();

            // Start it again
            var startResult = await Cli.RunCommandAsync("service start");
            startResult.Success.Should().BeTrue();

            // Give it a moment to start
            await Task.Delay(500);

            // Verify it's running
            var verifyResult = await Cli.RunCommandAsync("service status");
            verifyResult.Success.Should().BeTrue();
            verifyResult.Running.Should().BeTrue();
        }
        finally
        {
            // Ensure service is running and reconnected for other tests
            await Cli.RunCommandAsync("service start");
            await Task.Delay(300);
            await Cli.RunCommandAsync($"connect --pid {_fixture.TestAppPid}");
        }
    }
}
