namespace CodingAgentRunner.Quota;

/// <summary>
/// One quota window for a CLI subscription (e.g. monthly premium requests, a
/// 5-hour sliding window, or a weekly limit). All numeric fields are nullable
/// because each CLI exposes a different subset.
/// </summary>
public record QuotaWindow
{
    /// <summary>"Premium requests" / "5-hour" / "Weekly" / etc.</summary>
    public string Label { get; init; } = "";
    /// <summary>Percentage used, 0..100+. May exceed 100 when over-quota.</summary>
    public double? UsedPct { get; init; }
    /// <summary>Absolute used count when known.</summary>
    public double? Used { get; init; }
    /// <summary>Absolute plan limit when known.</summary>
    public double? Limit { get; init; }
    /// <summary>"requests" / "tokens" / "%".</summary>
    public string? Unit { get; init; }
    /// <summary>UTC timestamp when this window resets, when computable.</summary>
    public DateTime? ResetAt { get; init; }
    /// <summary>Original human-readable reset string from the CLI ("3:40am (Europe/Berlin)" / "Mar 1").</summary>
    public string? ResetLabel { get; init; }
}

/// <summary>
/// A single CLI's quota state at a point in time. <see cref="Error"/> is set when
/// probing failed; consumers should still display <see cref="Plan"/> and any
/// partial windows that did parse.
/// </summary>
public record QuotaSnapshot
{
    /// <summary>One of <see cref="Model.CliTypes"/>.</summary>
    public string CliType { get; init; } = "";
    /// <summary>UTC instant this snapshot was probed.</summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    /// <summary>"Pro" / "Pro+" / "Plus" / "Free" — null when unknown.</summary>
    public string? Plan { get; init; }
    /// <summary>The per-window usage for this CLI.</summary>
    public List<QuotaWindow> Windows { get; init; } = [];
    /// <summary>How the data was sourced: "/usage" / "/status" / "footer" / "banner".</summary>
    public string? Source { get; init; }
    /// <summary>Truncated raw snapshot for debugging.</summary>
    public string? RawSample { get; init; }
    /// <summary>Set when probing failed; <see cref="Plan"/>/<see cref="Windows"/> may still hold partial data.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// The highest <see cref="QuotaWindow.UsedPct"/> across all windows (0 when
    /// none are known). This is what the escalation caching keys on — the closer
    /// any window is to its limit, the more often the snapshot should be refreshed.
    /// </summary>
    public double MaxUsedPct => Windows.Count == 0 ? 0 : Windows.Max(w => w.UsedPct ?? 0);
}

/// <summary>A point-in-time report of every probed CLI's quota, plus the baseline cache TTL.</summary>
public record QuotaReport
{
    /// <summary>UTC instant the report was assembled.</summary>
    public DateTime At { get; init; } = DateTime.UtcNow;
    /// <summary>Baseline (non-escalated) cache TTL in seconds; consumers compute a "stale" badge from it.</summary>
    public int TtlSeconds { get; init; }
    /// <summary>One snapshot per known CLI.</summary>
    public List<QuotaSnapshot> Snapshots { get; init; } = [];
}
