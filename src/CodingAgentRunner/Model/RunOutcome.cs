namespace CodingAgentRunner.Model;

/// <summary>
/// How a run ended — the 3-valued terminal outcome carried by
/// <see cref="Events.CliRunEvent.RunEnded"/>. A deliberate <see cref="Stopped"/> is
/// NOT a <see cref="Failed"/>: distinguishing a wanted stop from a crash is the core
/// value of the library, so a binary success/error would be too coarse.
/// </summary>
public enum RunOutcome
{
    /// <summary>The CLI finished on its own, cleanly (exit 0, no runner stop).</summary>
    Completed,

    /// <summary>The runner stopped it on purpose (a <see cref="RunStopReason"/> was set) — not an error.</summary>
    Stopped,

    /// <summary>The CLI ended on its own with a non-zero / unknown exit — a self-crash.</summary>
    Failed,
}
