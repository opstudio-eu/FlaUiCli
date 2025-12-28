using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for window management commands (window list, window focus).
/// </summary>
[Collection("E2E")]
public class WindowTests : E2ETestBase
{
    public WindowTests(TestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task WindowList_ReturnsMainWindow()
    {
        var result = await Cli.RunCommandAsync("window list");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var windows = result.Data!.Value.EnumerateArray().ToList();
        windows.Should().NotBeEmpty();
        
        // Should contain the main window - API uses "title" not "name"
        var mainWindow = windows.FirstOrDefault(w =>
            w.TryGetProperty("title", out var title) &&
            title.GetString()?.Contains("FlaUI Test Application") == true);
        
        mainWindow.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined,
            "Main window should be in the window list");
    }

    [Fact]
    public async Task WindowFocus_WithValidId_Succeeds()
    {
        // Get window list first
        var listResult = await Cli.RunCommandAsync("window list");
        listResult.Success.Should().BeTrue();
        
        var windows = listResult.Data!.Value.EnumerateArray().ToList();
        windows.Should().NotBeEmpty();
        
        var windowId = windows[0].GetProperty("id").GetString();
        
        // Focus the window
        var focusResult = await Cli.RunCommandAsync($"window focus --id {windowId}");
        
        focusResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WindowFocus_WithInvalidId_FocusesMainWindow()
    {
        // API returns success:true even for invalid IDs (focuses main window by default)
        var result = await Cli.RunCommandAsync("window focus --id invalid-window-id");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("focused", out var focused).Should().BeTrue();
        focused.GetBoolean().Should().BeTrue();
    }
}
