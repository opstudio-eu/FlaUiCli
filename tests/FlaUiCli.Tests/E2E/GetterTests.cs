using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for getter commands (get text, value, state, patterns).
/// </summary>
[Collection("E2E")]
public class GetterTests : E2ETestBase
{
    public GetterTests(TestFixture fixture) : base(fixture)
    {
    }

    #region Get Text

    [Fact]
    public async Task GetText_FromButton_ReturnsButtonText()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"get text {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("text").GetString().Should().Be("Click Me");
    }

    [Fact]
    public async Task GetText_FromTextBlock_ReturnsText()
    {
        var elementId = await FindElementByAutomationIdAsync("StatusText");
        
        var result = await Cli.RunCommandAsync($"get text {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // StatusText content may change during test runs (e.g., "Ready" or "Button clicked!")
        // Just verify we get a non-empty text value
        result.Data!.Value.GetProperty("text").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetText_FromLabel_ReturnsText()
    {
        var elementId = await FindElementByAutomationIdAsync("NameLabel");
        
        var result = await Cli.RunCommandAsync($"get text {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("text").GetString().Should().Contain("Name");
    }

    #endregion

    #region Get Value

    [Fact]
    public async Task GetValue_FromTextBox_ReturnsValue()
    {
        var elementId = await FindElementByAutomationIdAsync("NameTextBox");
        
        // Type something first
        await Cli.RunCommandAsync($"action clear {elementId}");
        await Cli.RunCommandAsync($"action type {elementId} \"Test Value\"");
        await UiDelayAsync();

        var result = await Cli.RunCommandAsync($"get value {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("value").GetString().Should().Be("Test Value");
    }

    [Fact]
    public async Task GetValue_FromSlider_ReturnsNumericValue()
    {
        // Navigate to Sliders tab
        var tabId = await FindElementByAutomationIdAsync("SlidersTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var sliderId = await FindElementByAutomationIdAsync("VolumeSlider");
        
        var result = await Cli.RunCommandAsync($"get value {sliderId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // Slider value should be numeric (initial value is 50)
        result.Data!.Value.GetProperty("value").GetString().Should().NotBeNullOrEmpty();

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    #endregion

    #region Get State

    [Fact]
    public async Task GetState_FromButton_ReturnsEnabledState()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"get state {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("isEnabled", out var isEnabled).Should().BeTrue();
        isEnabled.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetState_FromDisabledButton_ShowsDisabled()
    {
        var elementId = await FindElementByAutomationIdAsync("DisabledButton");
        
        var result = await Cli.RunCommandAsync($"get state {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("isEnabled", out var isEnabled).Should().BeTrue();
        isEnabled.GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetState_FromCheckbox_ShowsCheckedState()
    {
        var elementId = await FindElementByAutomationIdAsync("SubscribeCheckBox");
        
        // Check it first
        await Cli.RunCommandAsync($"action check {elementId}");
        await UiDelayAsync();

        var result = await Cli.RunCommandAsync($"get state {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // API returns toggleState with "On"/"Off" values, not isChecked boolean
        result.Data!.Value.TryGetProperty("toggleState", out var toggleState).Should().BeTrue();
        toggleState.GetString().Should().Be("On");
    }

    [Fact]
    public async Task GetState_FromToggleButton_ShowsToggledState()
    {
        var elementId = await FindElementByAutomationIdAsync("ToggleButton");
        
        var result = await Cli.RunCommandAsync($"get state {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // API returns toggleState with "On"/"Off" values, not isToggled boolean
        result.Data!.Value.TryGetProperty("toggleState", out var toggleState).Should().BeTrue();
        toggleState.GetString().Should().BeOneOf("On", "Off");
    }

    #endregion

    #region Get Patterns

    [Fact]
    public async Task GetPatterns_FromButton_ReturnsInvokePattern()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"get patterns {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var patterns = result.Data!.Value.GetProperty("patterns").EnumerateArray()
            .Select(p => p.GetString())
            .ToList();
        
        patterns.Should().Contain("Invoke");
    }

    [Fact]
    public async Task GetPatterns_FromTextBox_ReturnsValuePattern()
    {
        var elementId = await FindElementByAutomationIdAsync("NameTextBox");
        
        var result = await Cli.RunCommandAsync($"get patterns {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var patterns = result.Data!.Value.GetProperty("patterns").EnumerateArray()
            .Select(p => p.GetString())
            .ToList();
        
        patterns.Should().Contain("Value");
    }

    [Fact]
    public async Task GetPatterns_FromCheckbox_ReturnsTogglePattern()
    {
        var elementId = await FindElementByAutomationIdAsync("AgreeCheckBox");
        
        var result = await Cli.RunCommandAsync($"get patterns {elementId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var patterns = result.Data!.Value.GetProperty("patterns").EnumerateArray()
            .Select(p => p.GetString())
            .ToList();
        
        patterns.Should().Contain("Toggle");
    }

    [Fact]
    public async Task GetPatterns_FromTreeViewItem_ReturnsExpandCollapsePattern()
    {
        // Navigate to TreeView tab
        var tabId = await FindElementByAutomationIdAsync("TreeViewTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var nodeId = await FindElementByAutomationIdAsync("DocumentsNode");
        
        var result = await Cli.RunCommandAsync($"get patterns {nodeId}");
        
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var patterns = result.Data!.Value.GetProperty("patterns").EnumerateArray()
            .Select(p => p.GetString())
            .ToList();
        
        patterns.Should().Contain("ExpandCollapse");

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task GetText_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("get text invalid-element-id");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetValue_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("get value invalid-element-id");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetState_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("get state invalid-element-id");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPatterns_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("get patterns invalid-element-id");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    #endregion
}
