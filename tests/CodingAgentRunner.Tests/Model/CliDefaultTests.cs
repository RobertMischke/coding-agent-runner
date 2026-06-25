using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Model;

public class CliDefaultTests
{
    [Fact]
    public void Resolve_FallsBackToGlobal_WhenNothingSeeded()
    {
        var d = new CliDefault<int>(global: 42);
        Assert.Equal(42, d.Resolve(new CliScope("codex", "gpt-5.5", "xhigh")));
    }

    [Fact]
    public void Resolve_MostSpecificWins_ThinkingBeatsCliBeatsGlobal()
    {
        var d = new CliDefault<int>(global: 1)
            .Set(new CliScope("codex"), 2)
            .Set(new CliScope("codex", ThinkingLevel: "xhigh"), 3);

        Assert.Equal(3, d.Resolve(new CliScope("codex", "gpt-5.5", "xhigh"))); // CLI + Thinking
        Assert.Equal(2, d.Resolve(new CliScope("codex", "gpt-5.5", "low")));   // falls back to CLI
        Assert.Equal(2, d.Resolve(new CliScope("codex")));                     // CLI
        Assert.Equal(1, d.Resolve(new CliScope("claude", "opus", "high")));    // global
    }

    [Fact]
    public void Resolve_CliPlusModel_BeatsCliOnly()
    {
        var d = new CliDefault<string>(global: "g")
            .Set(new CliScope("codex"), "cli")
            .Set(new CliScope("codex", "gpt-5.5"), "model");

        Assert.Equal("model", d.Resolve(new CliScope("codex", "gpt-5.5", "xhigh")));
        Assert.Equal("cli", d.Resolve(new CliScope("codex", "other", "xhigh")));
    }

    [Fact]
    public void Resolve_IsCaseInsensitiveOnScopeFields()
    {
        var d = new CliDefault<int>(global: 0).Set(new CliScope("codex", "GPT-5.5", "XHigh"), 9);
        Assert.Equal(9, d.Resolve(new CliScope("CODEX", "gpt-5.5", "xhigh")));
    }

    [Fact]
    public void Set_OverridesAnEarlierSeed()
    {
        var d = new CliDefault<int>(global: 0)
            .Set(new CliScope("codex"), 1)
            .Set(new CliScope("codex"), 2);
        Assert.Equal(2, d.Resolve(new CliScope("codex")));
    }

    [Fact]
    public void Set_RejectsEmptyCliType()
        => Assert.Throws<ArgumentException>(() => new CliDefault<int>(0).Set(new CliScope(""), 1));
}
