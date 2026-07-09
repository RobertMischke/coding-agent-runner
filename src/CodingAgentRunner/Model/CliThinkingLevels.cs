namespace CodingAgentRunner.Model;

/// <summary>
/// Capability table for CLI thinking / reasoning levels. Empty levels mean the
/// CLI/model has no supported selector and the runner should omit any flag.
/// </summary>
public static class CliThinkingLevels
{
    /// <summary>Lowest reasoning effort (OpenAI only).</summary>
    public const string Minimal = "minimal";
    /// <summary>Low reasoning effort.</summary>
    public const string Low = "low";
    /// <summary>Medium reasoning effort.</summary>
    public const string Medium = "medium";
    /// <summary>High reasoning effort.</summary>
    public const string High = "high";
    /// <summary>Extra-high reasoning effort (newer models only).</summary>
    public const string XHigh = "xhigh";
    /// <summary>
    /// Ultra reasoning effort — the top OpenAI/Codex rung, above <see cref="XHigh"/>
    /// (newest Codex models only, e.g. the gpt-5.6 family). Server-validated by Codex.
    /// </summary>
    public const string Ultra = "ultra";
    /// <summary>Maximum reasoning effort (select Claude models only).</summary>
    public const string Max = "max";

    private static readonly IReadOnlyList<string> OpenAiLevels = [Minimal, Low, Medium, High];
    private static readonly IReadOnlyList<string> OpenAiXHighLevels = [Minimal, Low, Medium, High, XHigh];
    // Ultra sits one rung above xhigh — the newest Codex families expose the full ladder.
    private static readonly IReadOnlyList<string> OpenAiUltraLevels = [Minimal, Low, Medium, High, XHigh, Ultra];
    private static readonly IReadOnlyList<string> ClaudeBasicLevels = [Low, Medium, High];
    private static readonly IReadOnlyList<string> ClaudeMaxLevels = [Low, Medium, High, Max];
    private static readonly IReadOnlyList<string> ClaudeXHighMaxLevels = [Low, Medium, High, XHigh, Max];

    /// <summary>The thinking / reasoning levels the given CLI + model supports. Empty means no selector.</summary>
    public static IReadOnlyList<string> For(string? cliType, string? model)
    {
        var cli = CliTypes.Normalize(cliType);
        var m = (model ?? string.Empty).Trim();

        if (string.Equals(cli, CliTypes.Codex, StringComparison.OrdinalIgnoreCase))
        {
            if (IsForeignCodexModel(m)) return [];
            if (IsUltraCapableCodexModel(m)) return OpenAiUltraLevels;
            return IsXHighCapableCodexModel(m) ? OpenAiXHighLevels : OpenAiLevels;
        }

        if (string.Equals(cli, CliTypes.Claude, StringComparison.OrdinalIgnoreCase))
        {
            var n = m.Replace('.', '-').ToLowerInvariant();
            // Only a real Claude model id (claude-…) gets a ladder. This gate rejects a
            // substring false-positive ("my-opus-4-8-clone") and a malformed id with
            // internal whitespace ("claude - opus - 4 - 8") — both fall back to no ladder.
            if (!n.StartsWith("claude-", StringComparison.Ordinal)) return [];
            if (n.Contains("haiku-4-5", StringComparison.Ordinal)) return [];
            if (n.Contains("opus-4-8", StringComparison.Ordinal)
                || n.Contains("opus-4-7", StringComparison.Ordinal))
                return ClaudeXHighMaxLevels;
            if (n.Contains("opus-4-6", StringComparison.Ordinal)
                || n.Contains("opus-4-5", StringComparison.Ordinal))
                return ClaudeMaxLevels;
            // Sonnet 5 supports the full ladder including xhigh and max (per the
            // Claude Code 2.1.x model metadata); the substring also covers point
            // releases like sonnet-5-5. Note "sonnet-4-5" does NOT match this.
            if (n.Contains("sonnet-5", StringComparison.Ordinal)) return ClaudeXHighMaxLevels;
            if (n.Contains("sonnet-4-6", StringComparison.Ordinal)) return ClaudeBasicLevels;
            if (n.StartsWith("claude-opus-", StringComparison.Ordinal)) return ClaudeMaxLevels;
            if (n.StartsWith("claude-sonnet-", StringComparison.Ordinal)) return ClaudeBasicLevels;
            return [];
        }

        return [];
    }

    /// <summary>
    /// Short human label for a level id (for a UI ladder or probe response). Unknown
    /// ids are echoed back trimmed so a new rung is never silently dropped from a UI.
    /// </summary>
    public static string DisplayName(string? level) => (level ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Minimal => "Minimal",
        Low => "Low",
        Medium => "Medium",
        High => "High",
        XHigh => "Extra High",
        Ultra => "Ultra",
        Max => "Max",
        _ => (level ?? string.Empty).Trim(),
    };

    /// <summary>The default thinking level for the given CLI + model, or null when there is no selector.</summary>
    public static string? DefaultFor(string? cliType, string? model)
    {
        var levels = For(cliType, model);
        if (levels.Count == 0) return null;
        return string.Equals(CliTypes.Normalize(cliType), CliTypes.Codex, StringComparison.OrdinalIgnoreCase)
            ? Medium
            : High;
    }

    /// <summary>Resolve a requested level against what the CLI + model supports, falling back to the default.</summary>
    public static string? Normalize(string? cliType, string? model, string? requested)
    {
        var levels = For(cliType, model);
        if (levels.Count == 0) return null;
        var value = string.IsNullOrWhiteSpace(requested)
            ? DefaultFor(cliType, model)
            : requested.Trim().ToLowerInvariant();
        if (value is null) return null;
        return levels.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? levels.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))
            : DefaultFor(cliType, model);
    }

    private static bool IsForeignCodexModel(string model)
    {
        // Normalize dots→dashes first so "claude.opus.4.8" is still recognized as foreign.
        var n = model.Replace('.', '-').ToLowerInvariant();
        return n.StartsWith("claude-", StringComparison.Ordinal)
               || n.StartsWith("gemini-", StringComparison.Ordinal);
    }

    /// <summary>
    /// Codex exposes the "Extra High" (<c>xhigh</c>) reasoning effort only on newer
    /// OpenAI models (gpt-5.5 and later). The codex <c>ReasoningEffort</c> enum
    /// serializes to lowercase, so the selector maps directly to
    /// <c>model_reasoning_effort="xhigh"</c>. Older codex models (gpt-5, gpt-5-codex)
    /// top out at <c>high</c>. Every ultra-capable model is also xhigh-capable.
    /// </summary>
    private static bool IsXHighCapableCodexModel(string model)
    {
        var m = model.Replace('.', '-').ToLowerInvariant();
        return m.Contains("gpt-5-5", StringComparison.Ordinal)   // gpt-5.5
               || IsUltraCapableCodexModel(model)                // gpt-5.6 family (also carries ultra)
               || m.Contains("gpt-6", StringComparison.Ordinal)
               || m.Contains("gpt-7", StringComparison.Ordinal);
    }

    /// <summary>
    /// Codex exposes the top <c>ultra</c> reasoning effort on its newest family, the
    /// gpt-5.6 models. LIVE evidence (codex-cli 0.144.0): <c>gpt-5.6-sol</c> accepts
    /// <c>model_reasoning_effort="ultra"</c> and rejects junk values server-side, so
    /// ultra is a real rung above xhigh. The prefix match (normalized <c>gpt-5-6</c>)
    /// covers <c>gpt-5.6-sol</c>, plain <c>gpt-5.6</c>, and future gpt-5.6 variants.
    /// Older families stay xhigh-capped until there is evidence they accept ultra.
    /// </summary>
    private static bool IsUltraCapableCodexModel(string model)
    {
        var m = model.Replace('.', '-').ToLowerInvariant();
        return m.Contains("gpt-5-6", StringComparison.Ordinal);  // gpt-5.6 family
    }
}
