using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for action commands (click, type, check, select, expand, etc.).
/// </summary>
[Collection("E2E")]
public class ActionTests : E2ETestBase
{
    public ActionTests(TestFixture fixture) : base(fixture)
    {
    }

    #region Click Actions

    [Fact]
    public async Task Click_OnButton_InvokesAction()
    {
        // Get initial click count
        var countId = await FindElementByAutomationIdAsync("ClickCountText");
        var initialText = await GetElementTextAsync(countId);
        var initialCount = int.Parse(initialText ?? "0");

        // Click the button
        await ClickByAutomationIdAsync("SimpleButton");
        await UiDelayAsync();

        // Verify click count increased
        var newText = await GetElementTextAsync(countId);
        var newCount = int.Parse(newText ?? "0");
        newCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task DoubleClick_OnElement_Works()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"action doubleclick {elementId}");
        
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RightClick_OnElement_Works()
    {
        // Navigate to Dialogs tab first where ContextMenuTarget is located
        var tabId = await FindElementByAutomationIdAsync("DialogsTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        // Right-click on the context menu target
        var elementId = await FindElementByAutomationIdAsync("ContextMenuTarget");
        
        var result = await Cli.RunCommandAsync($"action rightclick {elementId}");
        
        result.Success.Should().BeTrue();
        
        // Give time for context menu to appear, then dismiss it by clicking elsewhere
        await UiDelayAsync(300);
        
        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    [Fact]
    public async Task Invoke_OnButton_InvokesAction()
    {
        var countId = await FindElementByAutomationIdAsync("ClickCountText");
        var initialText = await GetElementTextAsync(countId);
        var initialCount = int.Parse(initialText ?? "0");

        var buttonId = await FindElementByAutomationIdAsync("SimpleButton");
        var result = await Cli.RunCommandAsync($"action invoke {buttonId}");
        
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        var newText = await GetElementTextAsync(countId);
        var newCount = int.Parse(newText ?? "0");
        newCount.Should().BeGreaterThan(initialCount);
    }

    #endregion

    #region Text Input Actions

    [Fact]
    public async Task Type_IntoTextBox_EntersText()
    {
        var elementId = await FindElementByAutomationIdAsync("NameTextBox");
        
        // Clear first
        await Cli.RunCommandAsync($"action clear {elementId}");
        await UiDelayAsync();

        // Type text
        var result = await Cli.RunCommandAsync($"action type {elementId} \"Hello World\"");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify text was entered
        var text = await GetElementValueAsync(elementId);
        text.Should().Be("Hello World");
    }

    [Fact]
    public async Task Clear_TextBox_RemovesText()
    {
        var elementId = await FindElementByAutomationIdAsync("NameTextBox");
        
        // First type something
        await Cli.RunCommandAsync($"action type {elementId} \"Test Text\"");
        await UiDelayAsync();

        // Clear it
        var result = await Cli.RunCommandAsync($"action clear {elementId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify text was cleared
        var text = await GetElementValueAsync(elementId);
        text.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Type_IntoMultilineTextBox_Works()
    {
        var elementId = await FindElementByAutomationIdAsync("DescriptionTextBox");
        
        // Clear first
        await Cli.RunCommandAsync($"action clear {elementId}");
        await UiDelayAsync();

        // Type text
        var result = await Cli.RunCommandAsync($"action type {elementId} \"Line 1\"");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        var text = await GetElementValueAsync(elementId);
        text.Should().Contain("Line 1");
    }

    #endregion

    #region Checkbox Actions

    [Fact]
    public async Task Check_Checkbox_SetsCheckedState()
    {
        var elementId = await FindElementByAutomationIdAsync("AgreeCheckBox");
        
        // Uncheck first to have known state
        await Cli.RunCommandAsync($"action uncheck {elementId}");
        await UiDelayAsync();

        // Check it
        var result = await Cli.RunCommandAsync($"action check {elementId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify checked state - API uses "toggleState" with values "On"/"Off"
        var state = await GetElementStateAsync(elementId);
        state.Should().NotBeNull();
        state!.Value.TryGetProperty("toggleState", out var toggleState).Should().BeTrue();
        toggleState.GetString().Should().Be("On");
    }

    [Fact]
    public async Task Uncheck_Checkbox_SetsUncheckedState()
    {
        var elementId = await FindElementByAutomationIdAsync("SubscribeCheckBox");
        
        // Check first to have known state
        await Cli.RunCommandAsync($"action check {elementId}");
        await UiDelayAsync();

        // Uncheck it
        var result = await Cli.RunCommandAsync($"action uncheck {elementId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify unchecked state - API uses "toggleState" with values "On"/"Off"
        var state = await GetElementStateAsync(elementId);
        state.Should().NotBeNull();
        state!.Value.TryGetProperty("toggleState", out var toggleState).Should().BeTrue();
        toggleState.GetString().Should().Be("Off");
    }

    [Fact]
    public async Task Toggle_ToggleButton_ChangesState()
    {
        var elementId = await FindElementByAutomationIdAsync("ToggleButton");
        
        // Get initial state - API uses "toggleState" with values "On"/"Off"
        var initialState = await GetElementStateAsync(elementId);
        var wasOn = initialState?.TryGetProperty("toggleState", out var toggled) == true && toggled.GetString() == "On";

        // Toggle it
        var result = await Cli.RunCommandAsync($"action toggle {elementId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify state changed
        var newState = await GetElementStateAsync(elementId);
        var isNowOn = newState?.TryGetProperty("toggleState", out var newToggled) == true && newToggled.GetString() == "On";
        isNowOn.Should().Be(!wasOn);
    }

    #endregion

    #region Selection Actions

    [Fact]
    public async Task Select_ComboBoxItem_SelectsItem()
    {
        // First navigate to the List Controls tab
        var tabId = await FindElementByAutomationIdAsync("ListControlsTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var comboId = await FindElementByAutomationIdAsync("ColorComboBox");
        
        // Select an item
        var result = await Cli.RunCommandAsync($"action select {comboId} \"Green\"");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Go back to basic controls tab for other tests
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    [Fact]
    public async Task Select_ListBoxItem_SelectsItem()
    {
        // Navigate to List Controls tab
        var tabId = await FindElementByAutomationIdAsync("ListControlsTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var listId = await FindElementByAutomationIdAsync("FruitListBox");
        
        // Select an item
        var result = await Cli.RunCommandAsync($"action select {listId} \"Cherry\"");
        result.Success.Should().BeTrue();

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    #endregion

    #region Expand/Collapse Actions

    [Fact]
    public async Task Expand_TreeViewNode_ExpandsNode()
    {
        // Navigate to TreeView tab
        var tabId = await FindElementByAutomationIdAsync("TreeViewTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var nodeId = await FindElementByAutomationIdAsync("DownloadsNode");
        
        // Collapse first to have known state
        await Cli.RunCommandAsync($"action collapse {nodeId}");
        await UiDelayAsync();

        // Expand it
        var result = await Cli.RunCommandAsync($"action expand {nodeId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify expanded state - API uses "expandCollapseState"
        var state = await GetElementStateAsync(nodeId);
        state.Should().NotBeNull();
        state!.Value.TryGetProperty("expandCollapseState", out var expandState).Should().BeTrue();
        expandState.GetString().Should().Be("Expanded");

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    [Fact]
    public async Task Collapse_TreeViewNode_CollapsesNode()
    {
        // Navigate to TreeView tab
        var tabId = await FindElementByAutomationIdAsync("TreeViewTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var nodeId = await FindElementByAutomationIdAsync("DocumentsNode");
        
        // Expand first to have known state
        await Cli.RunCommandAsync($"action expand {nodeId}");
        await UiDelayAsync();

        // Collapse it
        var result = await Cli.RunCommandAsync($"action collapse {nodeId}");
        result.Success.Should().BeTrue();
        await UiDelayAsync();

        // Verify collapsed state - API uses "expandCollapseState"
        var state = await GetElementStateAsync(nodeId);
        state.Should().NotBeNull();
        state!.Value.TryGetProperty("expandCollapseState", out var expandState).Should().BeTrue();
        expandState.GetString().Should().Be("Collapsed");

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    [Fact]
    public async Task Expand_Expander_ExpandsContent()
    {
        // Navigate to Sliders tab
        var tabId = await FindElementByAutomationIdAsync("SlidersTab");
        await Cli.RunCommandAsync($"action click {tabId}");
        await UiDelayAsync(500);

        var expanderId = await FindElementByAutomationIdAsync("DetailsExpander");
        
        // Collapse first
        await Cli.RunCommandAsync($"action collapse {expanderId}");
        await UiDelayAsync();

        // Expand it
        var result = await Cli.RunCommandAsync($"action expand {expanderId}");
        result.Success.Should().BeTrue();

        // Go back to basic controls tab
        var basicTabId = await FindElementByAutomationIdAsync("BasicControlsTab");
        await Cli.RunCommandAsync($"action click {basicTabId}");
        await UiDelayAsync(300);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Click_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("action click invalid-element-id");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Click_DisabledButton_Succeeds()
    {
        // Find the disabled button
        var elementId = await FindElementByAutomationIdAsync("DisabledButton");
        
        // Clicking a disabled button succeeds (the click action works, the button just doesn't respond)
        var result = await Cli.RunCommandAsync($"action click {elementId}");
        
        result.Success.Should().BeTrue();
    }

    #endregion
}
