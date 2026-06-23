namespace CodingAgentRunner.Execution;

/// <summary>
/// Everything the runner needs to start one CLI run. <see cref="RunId"/> is the
/// consumer's correlation key — it threads through every event and the output log.
/// </summary>
public sealed record CliRunRequest
{
    /// <summary>Consumer-assigned correlation id; unique among live runs of one driver.</summary>
    public required string RunId { get; init; }

    /// <summary>The prompt to hand the agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Working directory the CLI runs in (the checkout / worktree).</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Model id to invoke, or null for the CLI's default.</summary>
    public string? Model { get; init; }

    /// <summary>Requested thinking / reasoning level; resolved against the model's capability table.</summary>
    public string? ThinkingLevel { get; init; }

    /// <summary>CLI-native session id to resume (used only when <see cref="ResumeSession"/> is true).</summary>
    public string? SessionName { get; init; }

    /// <summary>Whether to resume <see cref="SessionName"/> rather than start a fresh session.</summary>
    public bool ResumeSession { get; init; }

    /// <summary>Permission posture (one of <see cref="Model.CliPermissionModes"/>); null normalizes to YOLO.</summary>
    public string? PermissionMode { get; init; }

    /// <summary>
    /// Context isolation (one of <see cref="Model.CliContextModes"/>). <b>Defaults to
    /// <c>clean</c></b> — an isolated per-run config home, so a run sees only the
    /// prompt + the versioned repo files. Set to <c>shared</c> to use the operator's
    /// global CLI state. CLIs that cannot isolate (Copilot/Gemini) run shared regardless.
    /// </summary>
    public string ContextMode { get; init; } = CodingAgentRunner.Model.CliContextModes.Clean;

    /// <summary>Extra environment variables for this run, applied after the standard hardening.</summary>
    public IReadOnlyDictionary<string, string>? ExtraEnvironment { get; init; }
}
