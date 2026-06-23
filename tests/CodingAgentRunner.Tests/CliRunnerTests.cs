using CodingAgentRunner;
using CodingAgentRunner.Drivers;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests;

public class CliRunnerTests
{
    [Fact]
    public void Get_ResolvesEachSupportedCli()
    {
        var runner = new CliRunner();
        Assert.IsType<ClaudeDriver>(runner.Get("claude"));
        Assert.IsType<CodexDriver>(runner.Get("codex"));
        Assert.IsType<GeminiDriver>(runner.Get("gemini"));
        Assert.IsType<CopilotDriver>(runner.Get("copilot"));
    }

    [Fact]
    public void Get_NormalizesTheCliType()
    {
        var runner = new CliRunner();
        Assert.Equal(CliTypes.Claude, runner.Get("CLAUDE").CliType);
    }

    [Fact]
    public void TryGet_UnknownFallsBackToNormalizedDefault_GetNeverThrowsForKnownTokens()
    {
        var runner = new CliRunner();
        // CliTypes.Normalize folds unknown values to copilot, so an odd token resolves.
        Assert.True(runner.TryGet("nonsense", out var driver));
        Assert.Equal(CliTypes.Copilot, driver.CliType);
    }

    [Fact]
    public void Drivers_CoverAllFour()
    {
        var runner = new CliRunner();
        Assert.Equal(4, runner.Drivers.Count);
    }
}
