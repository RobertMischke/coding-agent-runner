namespace CodingAgentRunner.Model;

/// <summary>
/// What a specific CLI + model can do — the answer to "which knobs should a UI
/// show for this run?". Returned by <see cref="Execution.ICliDriver.Capabilities"/>.
///
/// <para>
/// Reasoning effort is deliberately NOT generalized across CLIs: each CLI (and
/// often each model) has its own concept, so this reports the concrete levels
/// <em>this</em> CLI/model supports rather than a flattened global table. Anything
/// CLI-specific beyond reasoning lives in <see cref="Knobs"/> — an open,
/// forward-compatible extension point: a new per-CLI knob is one entry here plus a
/// <see cref="Execution.CliRunRequest.Tuning"/> key the driver reads, with no
/// breaking change to the contract.
/// </para>
/// </summary>
public sealed record CliCapabilities
{
    /// <summary>The CLI these capabilities describe (one of <see cref="CliTypes"/>).</summary>
    public string CliType { get; init; } = "";

    /// <summary>The model they were resolved for, or null for the CLI default.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Supported thinking / reasoning levels for this CLI + model (a subset of
    /// <see cref="CliThinkingLevels"/>). Empty means the CLI/model has no selector —
    /// a UI should hide the reasoning control entirely.
    /// </summary>
    public IReadOnlyList<string> ThinkingLevels { get; init; } = [];

    /// <summary>The level used when none is requested, or null when there is no selector.</summary>
    public string? DefaultThinkingLevel { get; init; }

    /// <summary>Whether this CLI can isolate per-run state (a <c>clean</c> context home).</summary>
    public bool SupportsCleanContext { get; init; }

    /// <summary>Whether this CLI can resume a prior session via <see cref="Execution.CliRunRequest.ResumeSessionId"/>.</summary>
    public bool SupportsResume { get; init; }

    /// <summary>
    /// Whether this CLI emits <see cref="Events.CliRunEvent.Heartbeat"/> liveness pings
    /// during extended thinking (Codex's reasoning at higher effort can run silent for
    /// minutes). A consumer reads this to widen the watchdog silence budget for such a
    /// run so "silent but pinging" is never mistaken for a hang. False for CLIs that go
    /// quiet while thinking (Claude / Gemini).
    /// </summary>
    public bool EmitsHeartbeatDuringThinking { get; init; }

    /// <summary>
    /// CLI-specific knobs beyond reasoning, as (knob-key → allowed values). The
    /// matching <see cref="Execution.CliRunRequest.Tuning"/> key feeds a chosen value
    /// back to the driver. Empty today for every built-in CLI — it is the seam for
    /// future per-CLI/per-model controls without changing the shared types.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Knobs { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Convenience: true when this CLI/model exposes any reasoning selector.</summary>
    public bool SupportsThinking => ThinkingLevels.Count > 0;
}
