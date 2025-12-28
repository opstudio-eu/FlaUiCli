using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for element discovery commands (element tree, find, info).
/// </summary>
[Collection("E2E")]
public class ElementTests : E2ETestBase
{
    public ElementTests(TestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ElementTree_ReturnsHierarchy()
    {
        var result = await Cli.RunCommandAsync("element tree --depth 2");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        // Data is an array of root elements (windows)
        var roots = result.Data!.Value.EnumerateArray().ToList();
        roots.Should().NotBeEmpty();
        
        // First root should have children
        var firstRoot = roots[0];
        firstRoot.TryGetProperty("children", out var children).Should().BeTrue();
        children.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ElementFind_ByAutomationId_FindsElement()
    {
        var result = await Cli.RunCommandAsync("element find --aid SimpleButton --first");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().HaveCount(1);
        
        var element = elements[0];
        element.GetProperty("automationId").GetString().Should().Be("SimpleButton");
        element.GetProperty("controlType").GetString().Should().Be("Button");
    }

    [Fact]
    public async Task ElementFind_ByName_FindsElement()
    {
        var result = await Cli.RunCommandAsync("element find --name \"Click Me\" --first");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ElementFind_ByType_FindsMultipleElements()
    {
        var result = await Cli.RunCommandAsync("element find --type Button");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Count.Should().BeGreaterThan(1, "There are multiple buttons in the test app");
    }

    [Fact]
    public async Task ElementFind_ByClass_FindsElements()
    {
        var result = await Cli.RunCommandAsync("element find --class Button");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ElementFind_NonExistent_ReturnsEmptyList()
    {
        var result = await Cli.RunCommandAsync("element find --aid NonExistentElement");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().BeEmpty();
    }

    [Fact]
    public async Task ElementInfo_WithValidId_ReturnsDetails()
    {
        // First find an element
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        // Get info
        var result = await Cli.RunCommandAsync($"element info {elementId}");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var info = result.Data!.Value;
        info.GetProperty("automationId").GetString().Should().Be("SimpleButton");
        info.GetProperty("controlType").GetString().Should().Be("Button");
        info.TryGetProperty("isEnabled", out var enabled).Should().BeTrue();
        enabled.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ElementInfo_WithInvalidId_ReturnsNullData()
    {
        // API returns success:true with data:null for invalid element IDs
        var result = await Cli.RunCommandAsync("element info invalid-element-id");

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ElementFind_WithParent_ScopesSearch()
    {
        // Find the Basic Controls tab panel first
        var tabResult = await Cli.RunCommandAsync("element find --name \"Basic Controls\" --type TabItem --first");
        tabResult.Success.Should().BeTrue();
        var tabs = tabResult.Data!.Value.EnumerateArray().ToList();
        tabs.Should().NotBeEmpty();
        
        var tabId = tabs[0].GetProperty("id").GetString();
        
        // Find buttons within that tab (should find SimpleButton)
        var result = await Cli.RunCommandAsync($"element find --type Button --parent {tabId}");
        
        result.Success.Should().BeTrue();
        var buttons = result.Data!.Value.EnumerateArray().ToList();
        buttons.Should().NotBeEmpty();
    }

    #region Element Tree Options

    [Fact]
    public async Task ElementTree_WithRoot_ScopesTree()
    {
        // First find a specific element to use as root
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        // Get tree starting from that element
        var result = await Cli.RunCommandAsync($"element tree --root {elementId} --depth 1");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        // The root element should be the button we specified
        var roots = result.Data!.Value.EnumerateArray().ToList();
        roots.Should().NotBeEmpty();
        
        var root = roots[0];
        root.GetProperty("automationId").GetString().Should().Be("SimpleButton");
    }

    [Fact]
    public async Task ElementTree_WithInvalidRoot_ReturnsEmptyOrError()
    {
        var result = await Cli.RunCommandAsync("element tree --root invalid-element-id --depth 1");

        // API may return success with empty data or an error for invalid root
        // Either behavior is acceptable
        if (result.Success)
        {
            // If success, data should be empty or null
            if (result.Data.HasValue)
            {
                var roots = result.Data.Value.EnumerateArray().ToList();
                roots.Should().BeEmpty();
            }
        }
        else
        {
            result.Error.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ElementTree_WithDepthZero_ReturnsOnlyRoot()
    {
        var result = await Cli.RunCommandAsync("element tree --depth 0");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var roots = result.Data!.Value.EnumerateArray().ToList();
        roots.Should().NotBeEmpty();
        
        // With depth 0, root elements should not have children populated
        // Children will be null or an empty array
        var firstRoot = roots[0];
        if (firstRoot.TryGetProperty("children", out var children))
        {
            // Children can be null or an empty array at depth 0
            if (children.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                children.EnumerateArray().Should().BeEmpty();
            }
            else
            {
                children.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            }
        }
    }

    #endregion

    #region Element Find Multiple Filters

    [Fact]
    public async Task ElementFind_ByAutomationIdAndType_FindsElement()
    {
        // Combine automation ID and type filters
        var result = await Cli.RunCommandAsync("element find --aid SimpleButton --type Button --first");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().HaveCount(1);
        
        var element = elements[0];
        element.GetProperty("automationId").GetString().Should().Be("SimpleButton");
        element.GetProperty("controlType").GetString().Should().Be("Button");
    }

    [Fact]
    public async Task ElementFind_ByNameAndType_FindsElement()
    {
        // Combine name and type filters
        var result = await Cli.RunCommandAsync("element find --name \"Click Me\" --type Button --first");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
        
        var element = elements[0];
        element.GetProperty("controlType").GetString().Should().Be("Button");
    }

    [Fact]
    public async Task ElementFind_ByTypeAndClass_FindsElements()
    {
        // Combine type and class filters
        var result = await Cli.RunCommandAsync("element find --type Button --class Button");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
        
        // All elements should be buttons
        foreach (var element in elements)
        {
            element.GetProperty("controlType").GetString().Should().Be("Button");
        }
    }

    [Fact]
    public async Task ElementFind_ByAutomationIdAndType_BothMatch_FindsElement()
    {
        // When both automation ID and type match, element is found
        var result = await Cli.RunCommandAsync("element find --aid SimpleButton --type Button");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
        
        // Verify the element matches both criteria
        var element = elements[0];
        element.GetProperty("automationId").GetString().Should().Be("SimpleButton");
        element.GetProperty("controlType").GetString().Should().Be("Button");
    }

    [Fact]
    public async Task ElementFind_WithParentAndType_ScopesCorrectly()
    {
        // Find a tab to scope the search
        var tabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        
        // Find checkboxes within that tab
        var result = await Cli.RunCommandAsync($"element find --type CheckBox --parent {tabId}");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().NotBeEmpty();
        
        // All elements should be checkboxes
        foreach (var element in elements)
        {
            element.GetProperty("controlType").GetString().Should().Be("CheckBox");
        }
    }

    [Fact]
    public async Task ElementFind_WithParentAndAutomationId_FindsSpecificElement()
    {
        // Find a tab to scope the search
        var tabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        
        // Find specific checkbox within that tab
        var result = await Cli.RunCommandAsync($"element find --aid AgreeCheckBox --parent {tabId} --first");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var elements = result.Data!.Value.EnumerateArray().ToList();
        elements.Should().HaveCount(1);
        elements[0].GetProperty("automationId").GetString().Should().Be("AgreeCheckBox");
    }

    #endregion
}
