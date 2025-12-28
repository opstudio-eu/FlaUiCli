using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for screenshot command.
/// </summary>
[Collection("E2E")]
public class ScreenshotTests : E2ETestBase
{
    private readonly string _tempDir;

    public ScreenshotTests(TestFixture fixture) : base(fixture)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FlaUiCliTests");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Screenshot_ToFile_SavesImage()
    {
        var outputPath = Path.Combine(_tempDir, $"test_screenshot_{Guid.NewGuid()}.png");
        
        try
        {
            var result = await Cli.RunCommandAsync($"screenshot --output \"{outputPath}\"");

            result.Success.Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue("Screenshot file should be created");
            
            var fileInfo = new FileInfo(outputPath);
            fileInfo.Length.Should().BeGreaterThan(0, "Screenshot file should not be empty");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task Screenshot_AsBase64_ReturnsBase64String()
    {
        var result = await Cli.RunCommandAsync("screenshot --base64");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var base64 = result.Data!.Value.GetProperty("base64").GetString();
        base64.Should().NotBeNullOrEmpty();
        
        // Verify it's valid base64
        var action = () => Convert.FromBase64String(base64!);
        action.Should().NotThrow("Should be valid base64 data");
        
        // Should be a PNG image (starts with PNG header when decoded)
        var bytes = Convert.FromBase64String(base64!);
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Screenshot_OfElement_CapturesElement()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        var outputPath = Path.Combine(_tempDir, $"element_screenshot_{Guid.NewGuid()}.png");
        
        try
        {
            var result = await Cli.RunCommandAsync($"screenshot --element {elementId} --output \"{outputPath}\"");

            result.Success.Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue("Element screenshot file should be created");
            
            var fileInfo = new FileInfo(outputPath);
            fileInfo.Length.Should().BeGreaterThan(0, "Element screenshot file should not be empty");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task Screenshot_OfElement_AsBase64_Works()
    {
        var elementId = await FindElementByAutomationIdAsync("SimpleButton");
        
        var result = await Cli.RunCommandAsync($"screenshot --element {elementId} --base64");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var base64 = result.Data!.Value.GetProperty("base64").GetString();
        base64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Screenshot_InvalidElement_ReturnsError()
    {
        var result = await Cli.RunCommandAsync("screenshot --element invalid-element-id --base64");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Screenshot_ToInvalidPath_ReturnsError()
    {
        // Try to save to an invalid path (non-existent drive or protected location)
        var result = await Cli.RunCommandAsync("screenshot --output \"Z:\\nonexistent\\path\\screenshot.png\"");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }
}
