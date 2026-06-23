using CodingAgentRunner.Quota;
using Xunit;

namespace CodingAgentRunner.Tests.Quota;

public class QuotaCacheOptionsTests
{
    private static QuotaSnapshot Snap(params double[] usedPcts) => new()
    {
        CliType = "claude",
        Windows = usedPcts.Select(p => new QuotaWindow { Label = "w", UsedPct = p }).ToList(),
    };

    [Theory]
    [InlineData(0, 600)]      // comfortable -> 10 min baseline
    [InlineData(50, 600)]
    [InlineData(89.9, 600)]   // just under the first tier
    [InlineData(90, 120)]     // >=90% -> 2 min
    [InlineData(95, 120)]
    [InlineData(96.9, 120)]
    [InlineData(97, 30)]      // >=97% -> 30 s
    [InlineData(120, 30)]     // over quota still uses the tightest tier
    public void EffectiveTtl_EscalatesWithUsage(double usedPct, int expectedSeconds)
    {
        var options = new QuotaCacheOptions();
        Assert.Equal(expectedSeconds, (int)options.EffectiveTtl(Snap(usedPct)).TotalSeconds);
    }

    [Fact]
    public void EffectiveTtl_UsesTheHighestWindow()
    {
        // A snapshot with one comfortable and one near-limit window escalates to the
        // near-limit window's tier.
        var options = new QuotaCacheOptions();
        Assert.Equal(30, (int)options.EffectiveTtl(Snap(10, 99, 40)).TotalSeconds);
    }

    [Fact]
    public void EffectiveTtl_NoWindows_UsesBaseline()
    {
        var options = new QuotaCacheOptions();
        Assert.Equal(options.DefaultTtl, options.EffectiveTtl(new QuotaSnapshot { CliType = "codex" }));
    }

    [Fact]
    public void EffectiveTtl_IsFullyConfigurable()
    {
        var options = new QuotaCacheOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(5),
            EscalationTiers = [new QuotaEscalationTier(50, TimeSpan.FromSeconds(10))],
        };
        Assert.Equal(TimeSpan.FromMinutes(5), options.EffectiveTtl(Snap(40)));
        Assert.Equal(TimeSpan.FromSeconds(10), options.EffectiveTtl(Snap(60)));
    }

    [Fact]
    public void EffectiveTtl_PicksShortestMatchingTier_RegardlessOfOrder()
    {
        var options = new QuotaCacheOptions
        {
            EscalationTiers =
            [
                new QuotaEscalationTier(97, TimeSpan.FromSeconds(30)),  // listed first on purpose
                new QuotaEscalationTier(90, TimeSpan.FromMinutes(2)),
            ],
        };
        Assert.Equal(30, (int)options.EffectiveTtl(Snap(98)).TotalSeconds);
    }
}
