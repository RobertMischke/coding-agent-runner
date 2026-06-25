namespace CodingAgentRunner.Model;

/// <summary>
/// The key a <see cref="CliDefault{T}"/> resolves against, in increasing
/// specificity. A null <see cref="Model"/> or <see cref="ThinkingLevel"/> means
/// "any": a default seeded at <c>(codex, null, null)</c> applies to every Codex run,
/// one seeded at <c>(codex, null, "xhigh")</c> only to Codex at xhigh.
/// </summary>
/// <param name="CliType">The CLI (one of <see cref="CliTypes"/>). Required.</param>
/// <param name="Model">The model id, or null for "any model".</param>
/// <param name="ThinkingLevel">The thinking level, or null for "any level".</param>
public readonly record struct CliScope(string CliType, string? Model = null, string? ThinkingLevel = null);

/// <summary>
/// A value the library ships a sensible default for, resolved by <see cref="CliScope"/>
/// specificity — the "batteries-included, overridable" mechanism. The library seeds the
/// normal knowledge (watchdog budgets, a default thinking level, …) with
/// <see cref="Set"/>; a consumer inherits it for free via <see cref="Resolve"/> and
/// overrides only the scope it cares about by calling <see cref="Set"/> again.
/// Most-specific scope wins; an unmatched scope falls back to the global default the
/// instance was created with.
/// </summary>
/// <typeparam name="T">The defaulted value (a budget, a level, a flag, …).</typeparam>
public sealed class CliDefault<T>
{
    private readonly Dictionary<CliScope, T> _entries = new(CliScopeComparer.Instance);
    private readonly T _global;

    /// <summary>Create a default carrying the global fallback used when no seeded scope matches.</summary>
    public CliDefault(T global) => _global = global;

    /// <summary>Seed (or override) the value for a scope; returns this for chaining.</summary>
    public CliDefault<T> Set(CliScope scope, T value)
    {
        if (string.IsNullOrWhiteSpace(scope.CliType))
            throw new ArgumentException("CliScope.CliType must be set.", nameof(scope));
        _entries[scope] = value;
        return this;
    }

    /// <summary>
    /// Resolve the value for a concrete run. Tries, most specific first:
    /// (CLI + Model + Thinking) → (CLI + Model) → (CLI + Thinking) → (CLI) → global.
    /// </summary>
    public T Resolve(CliScope scope)
    {
        foreach (var candidate in Candidates(scope))
            if (_entries.TryGetValue(candidate, out var v))
                return v;
        return _global;
    }

    private static IEnumerable<CliScope> Candidates(CliScope s)
    {
        yield return new CliScope(s.CliType, s.Model, s.ThinkingLevel);
        if (s.Model is not null)         yield return new CliScope(s.CliType, s.Model, null);
        if (s.ThinkingLevel is not null) yield return new CliScope(s.CliType, null, s.ThinkingLevel);
        yield return new CliScope(s.CliType, null, null);
    }
}

/// <summary>Case-insensitive structural equality for <see cref="CliScope"/>, treating a null model/level as "any".</summary>
internal sealed class CliScopeComparer : IEqualityComparer<CliScope>
{
    public static readonly CliScopeComparer Instance = new();
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;

    public bool Equals(CliScope x, CliScope y)
        => Cmp.Equals(x.CliType ?? "", y.CliType ?? "")
        && Cmp.Equals(x.Model ?? "", y.Model ?? "")
        && Cmp.Equals(x.ThinkingLevel ?? "", y.ThinkingLevel ?? "");

    public int GetHashCode(CliScope s) => HashCode.Combine(
        s.CliType is null ? 0 : Cmp.GetHashCode(s.CliType),
        s.Model is null ? 0 : Cmp.GetHashCode(s.Model),
        s.ThinkingLevel is null ? 0 : Cmp.GetHashCode(s.ThinkingLevel));
}
