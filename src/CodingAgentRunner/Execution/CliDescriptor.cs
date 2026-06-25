using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Events;
using CodingAgentRunner.Model;

namespace CodingAgentRunner.Execution;

/// <summary>Produces the <see cref="CliCapabilities"/> for a model — the descriptor seam that replaces a driver's <c>Capabilities</c> override.</summary>
public delegate CliCapabilities CliCapabilitiesProvider(string? model);

/// <summary>
/// Runs once before spawn to self-heal a known environment issue (e.g. Claude's npm
/// shim) so a run does not fail on a recoverable setup problem. Throws when the issue
/// is unrecoverable.
/// </summary>
public delegate void PreSpawnHealth(CliLaunchContext context);

/// <summary>
/// The one per-CLI value a consumer resolves by type and <em>uses</em> — a record of
/// pure data + delegates, with no base class, no vtable and nothing to override. Each
/// member is a seam that used to be a <c>protected virtual</c> on the driver base; a
/// new CLI is one of these registered in an <see cref="ICliCatalog"/>, not a subclass.
/// </summary>
public sealed record CliDescriptor
{
    /// <summary>The CLI this descriptor drives (one of <see cref="CliTypes"/>).</summary>
    public required string CliType { get; init; }

    /// <summary>Resolves the executable path/command from consumer options.</summary>
    public required Func<CliOptions, string> GetCliPath { get; init; }

    /// <summary>Builds the immutable launch spec for a run.</summary>
    public required LaunchSpecBuilder BuildLaunch { get; init; }

    /// <summary>Maps raw output lines onto typed events (model-blind).</summary>
    public required CliParser Parse { get; init; }

    /// <summary>Recognises stop conditions per line; the engine maps a verdict onto a <see cref="CliRunEvent.Interrupt"/>. Use <see cref="InterruptClassifiers.None"/> for a CLI with no special grammar.</summary>
    public required IInterruptClassifier InterruptClassifier { get; init; }

    /// <summary>How the engine measures liveness for this CLI (in-band vs a side-channel file).</summary>
    public required LivenessSpec Liveness { get; init; }

    /// <summary>Produces the capability table for a given model.</summary>
    public required CliCapabilitiesProvider Capabilities { get; init; }

    /// <summary>Whether a recorded session id is one this CLI can resume. Defaults to accepting any.</summary>
    public Func<string?, bool> CanResumeSessionId { get; init; } = static _ => true;

    /// <summary>Optional pre-spawn self-heal (e.g. Claude's npm-shim heal); null when the CLI needs none.</summary>
    public PreSpawnHealth? EnsureHealthy { get; init; }
}
