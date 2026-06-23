using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodingAgentRunner.Quota;

/// <summary>
/// Orchestrates the per-CLI <see cref="IQuotaProbe"/>s behind a cache with
/// <b>escalation</b>:
/// <list type="bullet">
///   <item>In-memory cache keyed by CLI type, hydrated from an optional
///         <see cref="IQuotaCacheStore"/> on construction.</item>
///   <item>Stale-while-revalidate: a cached snapshot is returned immediately and a
///         background re-probe refreshes it when stale.</item>
///   <item><b>Escalating TTL</b>: staleness uses
///         <see cref="QuotaCacheOptions.EffectiveTtl"/>, so a near-limit quota is
///         re-probed far more often than a comfortable one.</item>
///   <item>Concurrent re-probes for the same CLI are coalesced via per-CLI locks.</item>
/// </list>
/// Probing is expensive, so callers should rely on the cache and only force-refresh
/// on explicit intent.
/// <para>
/// <b>Thread-safe.</b> All members are safe to call concurrently from multiple
/// threads; refreshes are coalesced per CLI type. The returned <see cref="QuotaReport"/>
/// and <see cref="QuotaSnapshot"/> are immutable snapshots, safe to read without locks.
/// </para>
/// </summary>
public sealed class QuotaService
{
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<string, IQuotaProbe> _probes;
    private readonly ConcurrentDictionary<string, QuotaSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
    // In-flight probe per CLI. Lazy guarantees the probe body runs exactly once even
    // under a GetOrAdd race; every concurrent caller awaits the same task.
    private readonly ConcurrentDictionary<string, Lazy<Task<QuotaSnapshot?>>> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly QuotaCacheOptions _options;
    private readonly IQuotaCacheStore? _store;

    /// <summary>Create the service over a set of probes, with optional escalation options and persistence.</summary>
    public QuotaService(
        IEnumerable<IQuotaProbe> probes,
        QuotaCacheOptions? options = null,
        IQuotaCacheStore? store = null,
        ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _probes = probes.ToDictionary(p => p.CliType, StringComparer.OrdinalIgnoreCase);
        _options = options ?? new QuotaCacheOptions();
        _store = store;

        if (_store is not null)
        {
            try
            {
                foreach (var snap in _store.Read())
                    if (!string.IsNullOrWhiteSpace(snap.CliType)) _cache[snap.CliType] = snap;
                _logger.LogInformation("Hydrated quota cache from store ({Count} snapshots).", _cache.Count);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to hydrate quota cache from store"); }
        }
    }

    /// <summary>The CLI types this service has probes for.</summary>
    public IReadOnlyCollection<string> Probes => _probes.Keys.ToList();

    /// <summary>The baseline (non-escalated) cache TTL.</summary>
    public TimeSpan DefaultTtl => _options.DefaultTtl;

    /// <summary>The effective TTL for one CLI's current cached snapshot (or the baseline when none).</summary>
    public TimeSpan EffectiveTtlFor(string cliType)
        => _cache.TryGetValue(cliType, out var s) ? _options.EffectiveTtl(s) : _options.DefaultTtl;

    /// <summary>Every CLI's cached snapshot, without triggering any refresh.</summary>
    public QuotaReport GetCached()
        => new()
        {
            TtlSeconds = (int)_options.DefaultTtl.TotalSeconds,
            Snapshots = _probes.Keys
                .Select(k => _cache.TryGetValue(k, out var s) ? s : new QuotaSnapshot { CliType = k })
                .ToList(),
        };

    /// <summary>One CLI's cached snapshot without any refresh; null when never probed.</summary>
    public QuotaSnapshot? GetCachedFor(string cliType)
    {
        if (string.IsNullOrWhiteSpace(cliType)) return null;
        return _cache.TryGetValue(cliType, out var s) ? s : null;
    }

    /// <summary>Whether a CLI's cached snapshot is missing or past its effective TTL.</summary>
    public bool IsStale(string cliType)
        => !_cache.TryGetValue(cliType, out var s) || (DateTime.UtcNow - s.FetchedAt) > _options.EffectiveTtl(s);

    /// <summary>
    /// Return every cached snapshot immediately, kicking off a background re-probe
    /// for any that is missing or past its <b>effective</b> (escalation-aware) TTL.
    /// </summary>
    public QuotaReport GetWithBackgroundRefresh(CancellationToken ct = default)
    {
        foreach (var k in _probes.Keys)
            if (IsStale(k)) _ = RefreshAsync(k, ct);
        return GetCached();
    }

    /// <summary>Force a re-probe of every CLI and await all of them.</summary>
    public async Task<QuotaReport> RefreshAllAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(_probes.Keys.Select(k => RefreshAsync(k, ct))).ConfigureAwait(false);
        return GetCached();
    }

    /// <summary>
    /// Re-probe one CLI, replacing its cached snapshot. Coalesced: concurrent callers
    /// for the same CLI share ONE probe and all await its result (so a first probe is
    /// never duplicated, and a coalesced caller gets the fresh value rather than a
    /// stale cache). The shared probe is bounded by <see cref="QuotaCacheOptions.ProbeTimeout"/>;
    /// a single caller's <paramref name="ct"/> does not cancel the shared probe.
    /// </summary>
    public Task<QuotaSnapshot?> RefreshAsync(string cliType, CancellationToken ct = default)
    {
        if (!_probes.TryGetValue(cliType, out var probe)) return Task.FromResult<QuotaSnapshot?>(null);
        return _inflight.GetOrAdd(cliType,
            key => new Lazy<Task<QuotaSnapshot?>>(() => RunProbeAsync(key, probe))).Value;
    }

    private async Task<QuotaSnapshot?> RunProbeAsync(string cliType, IQuotaProbe probe)
    {
        try
        {
            using var cts = new CancellationTokenSource(_options.ProbeTimeout);
            var snap = await probe.ProbeAsync(cts.Token).ConfigureAwait(false);
            _cache[cliType] = snap;
            PersistCache();
            return snap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quota probe for {Cli} threw", cliType);
            var snap = new QuotaSnapshot { CliType = cliType, Error = ex.Message };
            _cache[cliType] = snap;
            PersistCache();
            return snap;
        }
        finally { _inflight.TryRemove(cliType, out _); }
    }

    private void PersistCache()
    {
        if (_store is null) return;
        try { _store.Write(_cache.Values); }
        catch (Exception ex) { _logger.LogDebug(ex, "Quota cache persist failed"); }
    }
}
