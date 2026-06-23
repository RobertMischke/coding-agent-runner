using CodingAgentRunner;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Model;

/// <summary>
/// Regression guard for the model-change contract: the SAME requested thinking level
/// resolves differently per model — every run normalizes against the model's capability
/// table, so swapping the model can never smuggle an unsupported level into the argv.
/// </summary>
public class ThinkingLevelNormalizationTests
{
    [Fact]
    public void XHigh_KeptOnOpus48_FallsToDefaultOnOpus46()
    {
        Assert.Equal("xhigh", CliThinkingLevels.Normalize("claude", "claude-opus-4-8", "xhigh"));
        // opus-4-6 tops out at max (no xhigh) → unsupported request falls back to the default (high).
        Assert.Equal("high", CliThinkingLevels.Normalize("claude", "claude-opus-4-6", "xhigh"));
    }

    [Fact]
    public void AnyLevel_OnHaiku_NormalizesToNull()   // no reasoning selector at all
    {
        Assert.Null(CliThinkingLevels.Normalize("claude", "claude-haiku-4-5", "high"));
        Assert.Null(CliThinkingLevels.Normalize("claude", "claude-haiku-4-5", "xhigh"));
    }

    [Fact]
    public void Codex_XHigh_OnGpt55_ButFallsToDefaultOnGpt5()
    {
        Assert.Equal("xhigh", CliThinkingLevels.Normalize("codex", "gpt-5.5", "xhigh"));
        Assert.Equal("medium", CliThinkingLevels.Normalize("codex", "gpt-5", "xhigh"));   // gpt-5 tops at high → default medium
    }

    [Fact]
    public void UnknownLevel_FallsBackToTheModelDefault()
    {
        Assert.Equal("high", CliThinkingLevels.Normalize("claude", "claude-opus-4-8", "ludicrous"));
        Assert.Equal("medium", CliThinkingLevels.Normalize("codex", "gpt-5.5", "ludicrous"));
    }

    [Fact]
    public void NullRequested_YieldsTheModelDefault()
    {
        Assert.Equal("high", CliThinkingLevels.Normalize("claude", "claude-opus-4-8", null));
        Assert.Equal("medium", CliThinkingLevels.Normalize("codex", "gpt-5.5", null));
    }

    [Fact]
    public void Capabilities_AdvertiseExactlyWhatNormalizeHonors()
    {
        var runner = new CliRunner();
        Assert.Contains("xhigh", runner.Claude.Capabilities("claude-opus-4-8").ThinkingLevels);
        Assert.DoesNotContain("xhigh", runner.Claude.Capabilities("claude-opus-4-6").ThinkingLevels);
        Assert.Empty(runner.Claude.Capabilities("claude-haiku-4-5").ThinkingLevels);
    }
}
