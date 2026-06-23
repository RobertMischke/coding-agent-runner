namespace CodingAgentRunner.Quota;

/// <summary>
/// A cap-enforcement verdict from <see cref="QuotaService.Gate"/>. Cheap and
/// non-blocking — it reflects the cache, never a fresh probe.
/// </summary>
/// <param name="Allowed">False when usage has reached the configured cap.</param>
/// <param name="Reason">Human-readable explanation when blocked; null when allowed.</param>
/// <param name="RetryAfter">Earliest known window reset, when blocked; null otherwise.</param>
public sealed record QuotaGate(bool Allowed, string? Reason, DateTime? RetryAfter)
{
    /// <summary>The permissive verdict: no cap configured, no cached data, or under the cap.</summary>
    public static QuotaGate Open { get; } = new(true, null, null);
}
