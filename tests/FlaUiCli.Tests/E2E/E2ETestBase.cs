using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Base class for E2E tests providing common helpers and access to the test fixture.
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase
{
    protected TestFixture Fixture { get; }
    protected CliRunner Cli => Fixture.Cli;

    protected E2ETestBase(TestFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Find an element by automation ID and return its element ID.
    /// </summary>
    protected async Task<string> FindElementByAutomationIdAsync(string automationId)
    {
        var result = await Cli.RunCommandAsync($"element find --aid {automationId} --first");
        result.Success.Should().BeTrue($"Failed to find element with automation ID '{automationId}': {result.ErrorMessage}");
        
        var elements = result.Data?.EnumerateArray().ToList();
        elements.Should().NotBeNull().And.HaveCountGreaterThan(0);
        
        return elements![0].GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Find an element by name and return its element ID.
    /// </summary>
    protected async Task<string> FindElementByNameAsync(string name)
    {
        var result = await Cli.RunCommandAsync($"element find --name \"{name}\" --first");
        result.Success.Should().BeTrue($"Failed to find element with name '{name}': {result.ErrorMessage}");
        
        var elements = result.Data?.EnumerateArray().ToList();
        elements.Should().NotBeNull().And.HaveCountGreaterThan(0);
        
        return elements![0].GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Get the text content of an element.
    /// </summary>
    protected async Task<string?> GetElementTextAsync(string elementId)
    {
        var result = await Cli.RunCommandAsync($"get text {elementId}");
        if (!result.Success)
        {
            return null;
        }
        return result.Data?.GetProperty("text").GetString();
    }

    /// <summary>
    /// Get the value of an element.
    /// </summary>
    protected async Task<string?> GetElementValueAsync(string elementId)
    {
        var result = await Cli.RunCommandAsync($"get value {elementId}");
        if (!result.Success)
        {
            return null;
        }
        return result.Data?.GetProperty("value").GetString();
    }

    /// <summary>
    /// Get element state information.
    /// </summary>
    protected async Task<JsonElement?> GetElementStateAsync(string elementId)
    {
        var result = await Cli.RunCommandAsync($"get state {elementId}");
        if (!result.Success)
        {
            return null;
        }
        return result.Data;
    }

    /// <summary>
    /// Click an element by its automation ID.
    /// </summary>
    protected async Task ClickByAutomationIdAsync(string automationId)
    {
        var elementId = await FindElementByAutomationIdAsync(automationId);
        var result = await Cli.RunCommandAsync($"action click {elementId}");
        result.Success.Should().BeTrue($"Failed to click element '{automationId}': {result.ErrorMessage}");
    }

    /// <summary>
    /// Type text into an element by its automation ID.
    /// </summary>
    protected async Task TypeByAutomationIdAsync(string automationId, string text)
    {
        var elementId = await FindElementByAutomationIdAsync(automationId);
        var result = await Cli.RunCommandAsync($"action type {elementId} \"{text}\"");
        result.Success.Should().BeTrue($"Failed to type into element '{automationId}': {result.ErrorMessage}");
    }

    /// <summary>
    /// Clear text from an element by its automation ID.
    /// </summary>
    protected async Task ClearByAutomationIdAsync(string automationId)
    {
        var elementId = await FindElementByAutomationIdAsync(automationId);
        var result = await Cli.RunCommandAsync($"action clear {elementId}");
        result.Success.Should().BeTrue($"Failed to clear element '{automationId}': {result.ErrorMessage}");
    }

    /// <summary>
    /// Small delay to allow UI to update.
    /// </summary>
    protected static Task UiDelayAsync(int milliseconds = 200)
    {
        return Task.Delay(milliseconds);
    }
}
