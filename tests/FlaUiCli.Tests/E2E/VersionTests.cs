using FluentAssertions;
using Xunit;

namespace FlaUiCli.Tests.E2E;

/// <summary>
/// Tests for the version command.
/// </summary>
[Collection("E2E")]
public class VersionTests
{
    private readonly TestFixture _fixture;
    private CliRunner Cli => _fixture.Cli;

    public VersionTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Version_ReturnsVersionString()
    {
        var result = await Cli.RunRawAsync("version");

        // Should be a valid semver-like version (e.g., "0.0.1" or "0.0.0-dev")
        result.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public async Task Version_OutputIsPlainText()
    {
        var result = await Cli.RunRawAsync("version");

        // Should not be JSON
        result.Should().NotStartWith("{");
        result.Should().NotStartWith("[");
        
        // Should be a simple version string without extra text
        result.Trim().Should().NotContain(" ");
    }
}
