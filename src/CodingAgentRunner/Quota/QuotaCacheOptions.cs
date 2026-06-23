namespace CodingAgentRunner.Quota;

/// <summary>
/// One escalation tier: once any quota window reaches <see cref="ThresholdPct"/>,
/// the cache TTL drops to <see cref="Ttl"/> so the snapshot is refreshed more
/// often as the limit gets close.
/// </summary>
public sealed record QuotaEscalationTier(double ThresholdPct, TimeSpan Ttl);

/// <summary>
/// Tunes the quota cache. The baseline <see cref="DefaultTtl"/> applies while usage
/// is comfortable; as any window crosses an escalation threshold the effective TTL
/// shortens, so a near-limit quota is polled more aggressively without paying that
/// cost the rest of the time.
///
/// <para>
/// Defaults: 10-minute baseline, tightening to 2 minutes at ≥90% used and to 30
/// seconds at ≥97% used. Every value is configurable, and the tiers can be replaced
/// wholesale.
/// </para>
/// </summary>
public sealed record QuotaCacheOptions
{
    /// <summary>Cache lifetime while no escalation tier is triggered. Default: 10 minutes.</summary>
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Escalation tiers, applied by usage. When several match, the shortest TTL
    /// wins. Default: ≥90% → 2 min, ≥97% → 30 s.
    /// </summary>
    public IReadOnlyList<QuotaEscalationTier> EscalationTiers { get; init; } =
    [
        new(90, TimeSpan.FromMinutes(2)),
        new(97, TimeSpan.FromSeconds(30)),
    ];

    /// <summary>Hard cap on a single probe. Default: 45 seconds.</summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// The effective cache TTL for <paramref name="snapshot"/>: the shortest TTL of
    /// every escalation tier whose threshold the snapshot's
    /// <see cref="QuotaSnapshot.MaxUsedPct"/> has reached, or <see cref="DefaultTtl"/>
    /// when none are reached.
    /// </summary>
    public TimeSpan EffectiveTtl(QuotaSnapshot snapshot)
    {
        var used = snapshot.MaxUsedPct;
        TimeSpan? tightest = null;
        foreach (var tier in EscalationTiers)
        {
            if (used < tier.ThresholdPct) continue;
            if (tightest is null || tier.Ttl < tightest.Value) tightest = tier.Ttl;
        }
        return tightest ?? DefaultTtl;
    }
}
