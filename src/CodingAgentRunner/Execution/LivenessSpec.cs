using CodingAgentRunner.Events;

namespace CodingAgentRunner.Execution;

/// <summary>Where a run's liveness signal comes from.</summary>
public enum LivenessChannel
{
    /// <summary>Activity is read from the in-band event stream (Claude / Codex / Gemini all stream frames).</summary>
    InBand,

    /// <summary>Activity is read from a side-channel file the CLI updates (e.g. a session JSONL whose mtime advances).</summary>
    SideChannelFile,
}

/// <summary>The inputs a <see cref="SideChannelProbe"/> needs to locate a run's side-channel activity file.</summary>
/// <param name="RunId">The run being probed.</param>
/// <param name="SessionId">The CLI-assigned session id, when known.</param>
/// <param name="WorkingDirectory">The run's working directory.</param>
/// <param name="CleanContextHome">The isolated config home for a clean run, when one was prepared.</param>
public readonly record struct SideChannelProbeContext(
    string RunId,
    string? SessionId,
    string WorkingDirectory,
    string? CleanContextHome);

/// <summary>
/// Reads the last-activity timestamp of a CLI's side-channel file (used when
/// <see cref="LivenessChannel.SideChannelFile"/>). Returns null when the file does
/// not exist yet. Pure I/O, no run state.
/// </summary>
/// <param name="LastActivityUtc">Probe the last-activity instant from the side-channel file.</param>
public sealed record SideChannelProbe(Func<SideChannelProbeContext, DateTime?> LastActivityUtc);

/// <summary>
/// How the engine measures whether a run is still alive. <see cref="IsActivitySignal"/>
/// decides which typed events reset the silence clock; for a
/// <see cref="LivenessChannel.SideChannelFile"/> CLI a <see cref="SideChannel"/> probe
/// also advances liveness from a file the in-band stream cannot see.
/// </summary>
public sealed record LivenessSpec
{
    /// <summary>Where liveness comes from.</summary>
    public required LivenessChannel Channel { get; init; }

    /// <summary>Which events count as activity (reset the silence clock). Defaults to the shared phase-aware rule.</summary>
    public Func<CliRunEvent, bool> IsActivitySignal { get; init; } = RunPhaseTransitions.IsActivitySignal;

    /// <summary>The side-channel file probe, when <see cref="Channel"/> is <see cref="LivenessChannel.SideChannelFile"/>; otherwise null.</summary>
    public SideChannelProbe? SideChannel { get; init; }

    /// <summary>The in-band liveness spec every streaming CLI uses: activity = the shared <see cref="RunPhaseTransitions.IsActivitySignal"/>.</summary>
    public static LivenessSpec InBandDefault { get; } = new() { Channel = LivenessChannel.InBand };
}
