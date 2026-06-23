using CodingAgentRunner.Quota;
using Xunit;

namespace CodingAgentRunner.Tests.Quota;

public class QuotaServiceTests
{
    private static QuotaSnapshot Snap(string cli, double usedPct, DateTime? fetchedAt = null) => new()
    {
        CliType = cli,
        FetchedAt = fetchedAt ?? DateTime.UtcNow,
        Windows = [new QuotaWindow { Label = "5-hour", UsedPct = usedPct }],
    };

    [Fact]
    public async Task RefreshAsync_PopulatesTheCache()
    {
        var calls = 0;
        var probe = new DelegateQuotaProbe("claude", _ => { calls++; return Task.FromResult(Snap("claude", 42)); });
        var svc = new QuotaService([probe]);

        var snap = await svc.RefreshAsync("claude");
        Assert.NotNull(snap);
        Assert.Equal(42, svc.GetCachedFor("claude")!.MaxUsedPct);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ProbeThatThrows_IsCachedAsAnErrorSnapshot()
    {
        var probe = new DelegateQuotaProbe("codex", _ => throw new InvalidOperationException("boom"));
        var svc = new QuotaService([probe]);

        var snap = await svc.RefreshAsync("codex");
        Assert.Equal("boom", snap!.Error);
        Assert.Equal("boom", svc.GetCachedFor("codex")!.Error);
    }

    [Fact]
    public void IsStale_UsesEscalationTtl()
    {
        // A snapshot fetched 3 minutes ago: comfortable usage is still fresh (10-min
        // baseline) but near-limit usage is already stale (30-s escalated TTL).
        var threeMinAgo = DateTime.UtcNow - TimeSpan.FromMinutes(3);
        var comfortable = new DelegateQuotaProbe("claude", _ => Task.FromResult(Snap("claude", 10, threeMinAgo)));
        var nearLimit = new DelegateQuotaProbe("codex", _ => Task.FromResult(Snap("codex", 99, threeMinAgo)));

        var svc = new QuotaService([comfortable, nearLimit]);
        // Seed the cache directly via a forced refresh (probes return the stamped time).
        svc.RefreshAllAsync().GetAwaiter().GetResult();

        Assert.False(svc.IsStale("claude"));  // 3 min < 10 min baseline
        Assert.True(svc.IsStale("codex"));    // 3 min > 30 s escalated
    }

    [Fact]
    public async Task GetCached_ReturnsAPlaceholderForUnprobedClis()
    {
        var probe = new DelegateQuotaProbe("claude", _ => Task.FromResult(Snap("claude", 5)));
        var svc = new QuotaService([probe]);

        var report = svc.GetCached();
        var only = Assert.Single(report.Snapshots);
        Assert.Equal("claude", only.CliType);
        Assert.Empty(only.Windows); // placeholder until probed

        await svc.RefreshAsync("claude");
        Assert.Equal(5, svc.GetCached().Snapshots.Single().MaxUsedPct);
    }

    [Fact]
    public async Task Persistence_HydratesFromTheStoreOnConstruction()
    {
        var path = Path.Combine(Path.GetTempPath(), "car-quota-" + Guid.NewGuid().ToString("N"), "cache.json");
        try
        {
            var store = new FileQuotaCacheStore(path);
            var writer = new QuotaService([new DelegateQuotaProbe("claude", _ => Task.FromResult(Snap("claude", 77)))], store: store);
            await writer.RefreshAsync("claude");

            // A fresh service over the same store sees the persisted snapshot immediately.
            var reader = new QuotaService([new DelegateQuotaProbe("claude", _ => Task.FromResult(Snap("claude", 0)))], store: store);
            Assert.Equal(77, reader.GetCachedFor("claude")!.MaxUsedPct);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { } }
    }
}
