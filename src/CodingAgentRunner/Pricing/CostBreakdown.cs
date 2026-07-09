namespace CodingAgentRunner.Pricing;

/// <summary>The outcome of resolving a price or computing a cost. Never implies a zero cost.</summary>
public enum PriceStatus
{
    /// <summary>A price entry valid at the requested time was found and used.</summary>
    Resolved,

    /// <summary>The model id did not resolve to any listing in the catalog.</summary>
    UnknownModel,

    /// <summary>
    /// The model is known, but no price entry is valid at or before the requested time. Covers a
    /// model with an empty history and a timestamp earlier than the first entry.
    /// </summary>
    NoPriceForDate,
}

/// <summary>
/// The result of <see cref="ModelPriceCatalog.ResolvePrice"/>: which entry (if any) applied, and
/// under what <see cref="Status"/>. When <see cref="Status"/> is not <see cref="PriceStatus.Resolved"/>,
/// <see cref="Price"/> is null — the caller must branch on the status rather than assume a price.
/// </summary>
public sealed record PriceResolution
{
    /// <summary>Why this resolution ended the way it did.</summary>
    public required PriceStatus Status { get; init; }

    /// <summary>The canonical model id when the model was found in the catalog; null for an unknown model.</summary>
    public string? ModelId { get; init; }

    /// <summary>The price entry in effect at the requested time, or null when none applied.</summary>
    public ModelPrice? Price { get; init; }

    /// <summary>True only when a concrete price was resolved.</summary>
    public bool Found => Status == PriceStatus.Resolved && Price is not null;
}

/// <summary>
/// A per-component cost breakdown plus a nullable <see cref="Total"/>. When no price applied
/// (<see cref="PriceStatus.UnknownModel"/> or <see cref="PriceStatus.NoPriceForDate"/>),
/// <see cref="Total"/> is null and the component figures are meaningless — a caller must check
/// <see cref="Status"/>/<see cref="HasPrice"/> and must never treat a missing price as zero cost.
/// </summary>
public sealed record CostBreakdown
{
    /// <summary>Whether a price was resolved, and if not, why.</summary>
    public required PriceStatus Status { get; init; }

    /// <summary>The canonical model id when the model was found; null for an unknown model.</summary>
    public string? ModelId { get; init; }

    /// <summary>The price entry used, or null when none applied.</summary>
    public ModelPrice? Price { get; init; }

    /// <summary>The currency the costs are quoted in, or null when no price applied.</summary>
    public string? Currency { get; init; }

    /// <summary>Cost of the input tokens. Meaningful only when <see cref="HasPrice"/>.</summary>
    public decimal InputCost { get; init; }

    /// <summary>Cost of the output tokens. Meaningful only when <see cref="HasPrice"/>.</summary>
    public decimal OutputCost { get; init; }

    /// <summary>Cost of the cache-read tokens. Meaningful only when <see cref="HasPrice"/>.</summary>
    public decimal CacheReadCost { get; init; }

    /// <summary>Cost of the cache-write tokens. Meaningful only when <see cref="HasPrice"/>.</summary>
    public decimal CacheWriteCost { get; init; }

    /// <summary>
    /// The sum of the component costs, or null when no price applied. Null is deliberate: an
    /// unpriced or unknown model yields <c>null</c>, never a silent <c>0</c> that would read as free.
    /// </summary>
    public decimal? Total { get; init; }

    /// <summary>True when a concrete price was applied and the component costs are meaningful.</summary>
    public bool HasPrice => Status == PriceStatus.Resolved;

    /// <summary>True when the applied price entry is a not-yet-confirmed placeholder (see <see cref="ModelPrice.Unconfirmed"/>).</summary>
    public bool Unconfirmed => Price?.Unconfirmed ?? false;
}
