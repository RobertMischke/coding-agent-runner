using CodingAgentRunner;
using CodingAgentRunner.Events;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class DescriptorValueTypesTests
{
    [Theory]
    [InlineData("stdout", CliStreamKind.Stdout)]
    [InlineData("STDERR", CliStreamKind.Stderr)]
    [InlineData("system", CliStreamKind.System)]
    [InlineData(null, CliStreamKind.Stdout)]
    [InlineData("nonsense", CliStreamKind.Stdout)]
    public void StreamKind_ParsesLeniently(string? input, CliStreamKind expected)
        => Assert.Equal(expected, CliStreamKinds.Parse(input));

    [Theory]
    [InlineData(CliStreamKind.Stdout)]
    [InlineData(CliStreamKind.Stderr)]
    [InlineData(CliStreamKind.System)]
    public void StreamKind_NameRoundTrips(CliStreamKind kind)
        => Assert.Equal(kind, CliStreamKinds.Parse(kind.Name()));

    [Fact]
    public void Liveness_InBandDefault_UsesSharedActivityRule()
    {
        var spec = LivenessSpec.InBandDefault;
        Assert.Equal(LivenessChannel.InBand, spec.Channel);
        Assert.Null(spec.SideChannel);
        // The default rule is the shared phase-aware one: an OutputDelta is activity,
        // a bare SessionInitializing is not.
        Assert.True(spec.IsActivitySignal(new CliRunEvent.OutputDelta("hi")));
        Assert.False(spec.IsActivitySignal(new CliRunEvent.SessionInitializing()));
    }

    [Fact]
    public void Capabilities_OnlyCodexEmitsHeartbeatDuringThinking()
    {
        var runner = new CliRunner();
        Assert.True(runner.Codex.Capabilities("gpt-5.5").EmitsHeartbeatDuringThinking);
        Assert.False(runner.Claude.Capabilities("claude-opus-4-8").EmitsHeartbeatDuringThinking);
        Assert.False(runner.Gemini.Capabilities(null).EmitsHeartbeatDuringThinking);
    }
}
