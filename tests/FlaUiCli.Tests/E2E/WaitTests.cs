using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for wait commands (wait element, gone, enabled).
/// </summary>
[Collection("E2E")]
public class WaitTests : E2ETestBase
{
    public WaitTests(TestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task WaitElement_ExistingElement_ReturnsImmediately()
    {
        var result = await Cli.RunCommandAsync("wait element --aid SimpleButton --timeout 5000");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        // Should return the found element
        var element = result.Data!.Value;
        element.GetProperty("automationId").GetString().Should().Be("SimpleButton");
    }

    [Fact]
    public async Task WaitElement_ByName_FindsElement()
    {
        var result = await Cli.RunCommandAsync("wait element --name \"Click Me\" --timeout 5000");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task WaitElement_ByType_FindsElement()
    {
        var result = await Cli.RunCommandAsync("wait element --type Button --timeout 5000");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task WaitElement_NonExistent_ReturnsError()
    {
        // Use a short timeout for faster test
        var result = await Cli.RunCommandAsync("wait element --aid NonExistentElement --timeout 500");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        // Error code is "COMMAND_ERROR", message contains timeout info
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task WaitEnabled_AlreadyEnabled_ReturnsImmediately()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"wait enabled {elementId} --timeout 5000");

        result.Success.Should().BeTrue();
        // API returns success:true with enabled:true for enabled elements
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("enabled", out var enabled).Should().BeTrue();
        enabled.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WaitEnabled_DisabledElement_ReturnsNotEnabled()
    {
        var elementId = await FindElementByAutomationIdAsync("DisabledButton");
        
        // API returns success:true with enabled:false (not an error)
        var result = await Cli.RunCommandAsync($"wait enabled {elementId} --timeout 500");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("enabled", out var enabled).Should().BeTrue();
        enabled.GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WaitGone_ExistingElement_ReturnsNotGone()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        // API returns success:true with gone:false (not an error)
        var result = await Cli.RunCommandAsync($"wait gone {elementId} --timeout 500");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("gone", out var gone).Should().BeTrue();
        gone.GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WaitGone_InvalidElement_ReturnsGone()
    {
        // An invalid element ID is considered "gone"
        var result = await Cli.RunCommandAsync("wait gone invalid-element-id --timeout 5000");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("gone", out var gone).Should().BeTrue();
        gone.GetBoolean().Should().BeTrue();
    }
}
