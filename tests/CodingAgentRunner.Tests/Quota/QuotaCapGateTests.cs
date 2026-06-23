using CodingAgentRunner.Events;
using CodingAgentRunner.Model;
using CodingAgentRunner.Quota;
using Xunit;

namespace CodingAgentRunner.Tests.Quota;

public class QuotaCapGateTests
{
    // A probe that returns a fixed snapshot, so we can seed the cache deterministically.
    private static QuotaService WithSnapshot(string cli, double usedPct, DateTime? reset = null)
    {
        var probe = new DelegateQuotaProbe(cli, _ => Task.FromResult(new QuotaSnapshot
        {
            CliType = cli,
            Windows = [new QuotaWindow { Label = "5-hour", UsedPct = usedPct, ResetAt = reset }],
        }));
        var svc = new QuotaService([probe]);
        svc.RefreshAllAsync().GetAwaiter().GetResult();   // populate the cache
        return svc;
    }

    [Fact]
    public void NoCap_Configured_IsAlwaysOpen()
    {
        var svc = WithSnapshot("claude", 99);
        Assert.True(svc.Gate("claude").Allowed);   // 99% used but no cap set
    }

    [Fact]
    public void UnderCap_IsOpen_AtOrOverCap_IsBlocked_WithReset()
    {
        var reset = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var svc = WithSnapshot("claude", 96, reset);
        svc.Cap("claude", stopAtPercent: 95);

        var gate = svc.Gate("claude");
        Assert.False(gate.Allowed);
        Assert.Contains("96%", gate.Reason);
        Assert.Equal(reset, gate.RetryAfter);
        Assert.True(svc.IsAtCap("claude"));
    }

    [Fact]
    public void Cap_IsCaseInsensitive_AndPerCli()
    {
        var svc = WithSnapshot("claude", 96);
        svc.Cap("CLAUDE", 95);
        Assert.False(svc.Gate("claude").Allowed);   // normalized
        Assert.True(svc.Gate("codex").Allowed);     // unrelated CLI, no cap, no data
    }

    [Fact]
    public void NoData_FailsOpen_EvenWithACap()
    {
        var svc = new QuotaService(System.Array.Empty<IQuotaProbe>());
        svc.Cap("claude", 90);
        Assert.True(svc.Gate("claude").Allowed);    // capped but never probed → don't block
    }

    [Fact]
    public void Observe_Overage_MarksAtCap_FromAFreeEvent()
    {
        var svc = new QuotaService(System.Array.Empty<IQuotaProbe>());
        svc.Cap("claude", 100);

        long resetUnix = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var harvested = svc.Observe("claude", new CliRunEvent.RateLimitObserved(
            Window: "5-hour", Status: "rejected", ResetsAt: resetUnix,
            OverageStatus: "over", IsUsingOverage: true));

        Assert.True(harvested);
        var snap = svc.GetCachedFor("claude");
        Assert.NotNull(snap);
        Assert.Equal("event", snap!.Source);
        Assert.Equal(100, snap.MaxUsedPct);
        Assert.False(svc.Gate("claude").Allowed);   // overage → at the 100% cap, no probe needed
    }

    [Fact]
    public void Observe_IgnoresNonRateLimitEvents()
    {
        var svc = new QuotaService(System.Array.Empty<IQuotaProbe>());
        Assert.False(svc.Observe("claude", new CliRunEvent.OutputDelta("hi")));
        Assert.Null(svc.GetCachedFor("claude"));
    }

    [Fact]
    public void Observe_DoesNotLower_AKnownUsage()
    {
        var svc = WithSnapshot("claude", 95);   // a probe established 95%
        // A non-overage event arrives (carries no percent): must NOT drop usage to 0.
        svc.Observe("claude", new CliRunEvent.RateLimitObserved(
            Window: "5-hour", Status: "allowed", ResetsAt: 0, OverageStatus: null, IsUsingOverage: false));
        Assert.Equal(95, svc.GetCachedFor("claude")!.MaxUsedPct);
    }
}
