using CodingAgentRunner.Events;
using CodingAgentRunner.Execution;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class InterruptClassifierTests
{
    private static InterruptContext Ctx => new("run-1", RunPhase.PromptConsumed, 0, "codex");

    [Fact]
    public void None_NeverInterrupts()
    {
        var ctx = Ctx;
        Assert.Null(InterruptClassifiers.None.Classify("CreateProcessAsUserW failed", in ctx));
    }

    [Fact]
    public void EnvironmentBlocker_MatchesKnownBlocker_Fatal()
    {
        var ctx = Ctx;
        var v = InterruptClassifiers.EnvironmentBlocker()
            .Classify("node: EACCES: permission denied, open '/x'", in ctx);
        Assert.NotNull(v);
        Assert.Equal(InterruptReason.EnvironmentBlocker, v!.Kind);
        Assert.True(v.IsFatal);
    }

    [Fact]
    public void EnvironmentBlocker_IgnoresOrdinaryOutput_AndEmpty()
    {
        var ctx = Ctx;
        var c = InterruptClassifiers.EnvironmentBlocker();
        Assert.Null(c.Classify("All good, build succeeded.", in ctx));
        Assert.Null(c.Classify("", in ctx));
    }

    [Fact]
    public void EnvironmentBlocker_CustomPatterns_OnlyMatchTheirOwnSet()
    {
        var ctx = Ctx;
        var c = InterruptClassifiers.EnvironmentBlocker(new[] { "sandbox denied" });
        Assert.NotNull(c.Classify("error: SANDBOX DENIED by policy", in ctx));
        Assert.Null(c.Classify("EACCES", in ctx)); // not in the custom set
    }

    [Fact]
    public void Composite_FirstNonNullWins()
    {
        var ctx = Ctx;
        var quota = InterruptClassifiers.Predicate((line, _) =>
            line.Contains("quota") ? new InterruptSignal(InterruptReason.QuotaExhausted, "quota", true) : null);
        var c = InterruptClassifiers.Composite(
            InterruptClassifiers.None, quota, InterruptClassifiers.EnvironmentBlocker());

        Assert.Equal(InterruptReason.QuotaExhausted, c.Classify("hit quota wall", in ctx)!.Kind);
        Assert.Equal(InterruptReason.EnvironmentBlocker, c.Classify("EACCES denied", in ctx)!.Kind);
        Assert.Null(c.Classify("normal line", in ctx));
    }

    [Fact]
    public void Signal_ToEvent_MapsFieldsAndRunId()
    {
        var ev = new InterruptSignal(InterruptReason.QuotaExhausted, "5h cap", IsFatal: true).ToEvent("run-9");
        Assert.Equal(InterruptReason.QuotaExhausted, ev.Reason);
        Assert.Equal("5h cap", ev.Detail);
        Assert.True(ev.IsFatal);
        Assert.Equal("run-9", ev.RunId);
    }
}
