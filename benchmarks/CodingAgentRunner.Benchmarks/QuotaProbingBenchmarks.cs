using BenchmarkDotNet.Attributes;
using CodingAgentRunner.Adapters;
using CodingAgentRunner.Events;
using CodingAgentRunner.Model;
using CodingAgentRunner.Quota;

namespace CodingAgentRunner.Benchmarks;

/// <summary>
/// Cost of the quota-probing hot paths: parsing the Claude usage-endpoint
/// response, parsing a Codex rollout line, the Codex probe's newest-file scan
/// over a realistic sessions directory, the free event harvest
/// (<see cref="QuotaService.Observe"/>, which runs once per
/// <c>RateLimitObserved</c> during a live run), and one shared-store
/// read-merge-write round trip. Network latency of the Claude probe's HTTP call
/// is deliberately out of scope — these measure the library's own work.
/// </summary>
[MemoryDiagnoser]
public class QuotaProbingBenchmarks
{
    // Response shape captured live from api.anthropic.com/api/oauth/usage (values representative).
    private const string ClaudeUsageJson = """
    {
      "five_hour":  { "utilization": 14.0, "resets_at": "2026-07-02T21:10:00.549926+00:00", "limit_dollars": null, "used_dollars": null },
      "seven_day":  { "utilization": 5.0,  "resets_at": "2026-07-07T11:59:59.549952+00:00", "limit_dollars": null, "used_dollars": null },
      "limits": [
        { "kind": "session",       "group": "session", "percent": 14, "severity": "normal", "resets_at": "2026-07-02T21:10:00.549926+00:00", "scope": null, "is_active": true },
        { "kind": "weekly_all",    "group": "weekly",  "percent": 5,  "severity": "normal", "resets_at": "2026-07-07T11:59:59.549952+00:00", "scope": null, "is_active": false },
        { "kind": "weekly_scoped", "group": "weekly",  "percent": 8,  "severity": "normal", "resets_at": "2026-07-07T12:00:00.550355+00:00",
          "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null }, "is_active": false }
      ],
      "extra_usage": { "is_enabled": true, "monthly_limit": 4250, "used_credits": 0.0, "utilization": null, "currency": "EUR" }
    }
    """;

    // Rollout line shape captured live from ~/.codex/sessions (codex 0.142).
    private const string CodexRolloutLine =
        """{"timestamp":"2026-07-02T17:31:07.403Z","type":"event_msg","payload":{"type":"token_count","info":null,"rate_limits":{"limit_id":"codex","limit_name":null,"primary":{"used_percent":12.5,"window_minutes":300,"resets_at":1783031434},"secondary":{"used_percent":3.0,"window_minutes":10080,"resets_at":1783618234},"credits":null,"individual_limit":null,"plan_type":"pro","rate_limit_reached_type":null}}}""";

    private const string CodexTokenCountFrame =
        """{"type":"token_count","info":null,"rate_limits":{"limit_id":"codex","limit_name":null,"primary":{"used_percent":12.5,"window_minutes":300,"resets_at":1783031434},"secondary":{"used_percent":3.0,"window_minutes":10080,"resets_at":1783618234},"credits":null,"individual_limit":null,"plan_type":"pro","rate_limit_reached_type":null}}""";

    private string _codexHome = "";
    private string _storeDir = "";
    private CodexSessionLogProbe _codexProbe = null!;
    private FileQuotaCacheStore _store = null!;
    private QuotaService _observeService = null!;
    private CliRunEvent.RateLimitObserved _rateLimitEvent = null!;
    private List<QuotaSnapshot> _storePayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A realistic sessions tree: 3 day-directories × 2 rollouts, ~400 lines each,
        // the token_count entries buried mid-file like a real run writes them.
        _codexHome = Path.Combine(Path.GetTempPath(), "car-bench-codex-" + Guid.NewGuid().ToString("N"));
        for (var day = 1; day <= 3; day++)
        {
            var dir = Path.Combine(_codexHome, "sessions", "2026", "07", day.ToString("00"));
            Directory.CreateDirectory(dir);
            for (var f = 0; f < 2; f++)
            {
                var lines = new List<string>();
                for (var i = 0; i < 400; i++)
                {
                    lines.Add(i % 50 == 25
                        ? CodexRolloutLine
                        : """{"timestamp":"2026-07-02T17:31:07.000Z","type":"event_msg","payload":{"type":"agent_message","message":"filler line for realistic scan cost"}}""");
                }
                File.WriteAllLines(Path.Combine(dir, $"rollout-2026-07-{day:00}T10-0{f}-00-bench.jsonl"), lines);
            }
        }
        _codexProbe = new CodexSessionLogProbe(codexHome: _codexHome);

        _storeDir = Path.Combine(Path.GetTempPath(), "car-bench-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storeDir);
        _store = new FileQuotaCacheStore(Path.Combine(_storeDir, "quota-cache.json"));
        _storePayload =
        [
            ClaudeOAuthUsageProbe.ParseUsage(ClaudeUsageJson, plan: "max"),
            CodexSessionLogProbe.TryParseRolloutLine(CodexRolloutLine, out var s) ? s! : throw new InvalidOperationException(),
        ];

        _observeService = new QuotaService(probes: []);
        _rateLimitEvent = (CliRunEvent.RateLimitObserved)CodexEventAdapter.Map(CodexTokenCountFrame, "bench").First();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_codexHome, recursive: true); } catch { }
        try { Directory.Delete(_storeDir, recursive: true); } catch { }
    }

    /// <summary>Claude usage-endpoint response → QuotaSnapshot (once per probe, after the HTTP call).</summary>
    [Benchmark(Baseline = true)]
    public QuotaSnapshot ClaudeParseUsage() => ClaudeOAuthUsageProbe.ParseUsage(ClaudeUsageJson, plan: "max");

    /// <summary>One Codex rollout line → QuotaSnapshot (runs per scanned line that mentions rate_limits).</summary>
    [Benchmark]
    public bool CodexParseRolloutLine() => CodexSessionLogProbe.TryParseRolloutLine(CodexRolloutLine, out _);

    /// <summary>The full Codex probe: enumerate newest rollouts, scan backwards, parse (once per probe).</summary>
    [Benchmark]
    public Task<QuotaSnapshot> CodexProbeFileScan() => _codexProbe.ProbeAsync(CancellationToken.None);

    /// <summary>Codex token_count frame → per-window RateLimitObserved events (adapter path).</summary>
    [Benchmark]
    public int CodexAdapterTokenCount()
    {
        var count = 0;
        foreach (var _ in CodexEventAdapter.Map(CodexTokenCountFrame, "bench")) count++;
        return count;
    }

    /// <summary>The free event harvest into the cache (once per RateLimitObserved of a live run).</summary>
    [Benchmark]
    public bool ObserveHarvest() => _observeService.Observe(CliTypes.Codex, _rateLimitEvent);

    /// <summary>Shared-store round trip: read-merge-write both snapshots, then read back (once per persist).</summary>
    [Benchmark]
    public int StoreWriteRead()
    {
        _store.Write(_storePayload);
        return _store.Read().Count;
    }
}
