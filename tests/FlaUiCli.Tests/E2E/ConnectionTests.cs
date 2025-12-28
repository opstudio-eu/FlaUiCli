using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for connection management commands (connect, disconnect, status).
/// </summary>
[Collection("E2E")]
public class ConnectionTests : E2ETestBase
{
    public ConnectionTests(TestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Status_WhenConnected_ShowsConnectionInfo()
    {
        var result = await Cli.RunCommandAsync("status");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var data = result.Data!.Value;
        data.GetProperty("connected").GetBoolean().Should().BeTrue();
        data.GetProperty("processId").GetInt32().Should().Be(Fixture.TestAppPid);
    }

    [Fact]
    public async Task Connect_WithInvalidPid_ReturnsError()
    {
        // First disconnect
        await Cli.RunCommandAsync("disconnect");
        
        try
        {
            // Try to connect to a non-existent process
            var result = await Cli.RunCommandAsync("connect --pid 999999");

            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNull();
        }
        finally
        {
            // Reconnect for other tests
            await Cli.RunCommandAsync($"connect --pid {Fixture.TestAppPid}");
        }
    }

    [Fact]
    public async Task Connect_ByName_Succeeds()
    {
        // First disconnect
        await Cli.RunCommandAsync("disconnect");
        
        try
        {
            // Connect by process name
            var result = await Cli.RunCommandAsync("connect --name FlaUiTestApp");

            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }
        finally
        {
            // Ensure we're connected for other tests
            var status = await Cli.RunCommandAsync("status");
            if (!status.Success || !status.Data!.Value.GetProperty("connected").GetBoolean())
            {
                await Cli.RunCommandAsync($"connect --pid {Fixture.TestAppPid}");
            }
        }
    }

    [Fact]
    public async Task ProcessList_ReturnsProcesses()
    {
        var result = await Cli.RunCommandAsync("process list");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        // Should contain at least our test app
        var processes = result.Data!.Value.EnumerateArray().ToList();
        processes.Should().NotBeEmpty();
        
        // Find our test app in the list
        var testApp = processes.FirstOrDefault(p => 
            p.TryGetProperty("name", out var name) && 
            name.GetString()?.Contains("FlaUiTestApp") == true);
        
        testApp.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined, 
            "TestApp should be in the process list");
    }

    [Fact]
    public async Task Connect_ByInvalidName_ReturnsError()
    {
        // First disconnect
        await Cli.RunCommandAsync("disconnect");
        
        try
        {
            // Try to connect to a non-existent process by name
            var result = await Cli.RunCommandAsync("connect --name NonExistentProcessName12345");

            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNull();
        }
        finally
        {
            // Reconnect for other tests
            await Cli.RunCommandAsync($"connect --pid {Fixture.TestAppPid}");
        }
    }

    [Fact]
    public async Task Disconnect_WhenConnected_Succeeds()
    {
        try
        {
            // Disconnect
            var result = await Cli.RunCommandAsync("disconnect");
            result.Success.Should().BeTrue();

            // Verify disconnected
            var status = await Cli.RunCommandAsync("status");
            status.Success.Should().BeTrue();
            status.Data!.Value.GetProperty("connected").GetBoolean().Should().BeFalse();
        }
        finally
        {
            // Reconnect for other tests
            await Cli.RunCommandAsync($"connect --pid {Fixture.TestAppPid}");
        }
    }

    [Fact]
    public async Task Disconnect_WhenNotConnected_Succeeds()
    {
        // First disconnect
        await Cli.RunCommandAsync("disconnect");
        
        try
        {
            // Disconnect again - should still succeed
            var result = await Cli.RunCommandAsync("disconnect");
            result.Success.Should().BeTrue();
        }
        finally
        {
            // Reconnect for other tests
            await Cli.RunCommandAsync($"connect --pid {Fixture.TestAppPid}");
        }
    }
}
