namespace CodingAgentRunner.Events;

/// <summary>
/// Why a run should be interrupted — a stop condition the library recognises in a
/// CLI's output. Each value is a distinct recovery story, so a host can react
/// (stop, park, retry-after-reset) by kind rather than by re-parsing a string.
/// Carried by <see cref="CliRunEvent.Interrupt"/> on the event stream, and by
/// <see cref="Execution.InterruptSignal"/> when a classifier first recognises it.
/// </summary>
public enum InterruptReason
{
    /// <summary>A runtime / sandbox / OS error the agent cannot self-recover from (denied process creation, EACCES, a network block). Continuing only burns the silence budget against the same wall.</summary>
    EnvironmentBlocker,

    /// <summary>The provider's usage quota is exhausted. Recoverable after the window resets — unlike an environment blocker.</summary>
    QuotaExhausted,

    /// <summary>A consumer-defined completion sentinel appeared in the output.</summary>
    Sentinel,

    /// <summary>The agent echoed a control phrase about its own output (the scanner self-reference trap). Recognised so it is NOT mistaken for a real blocker; a classifier returns it with <c>IsFatal: false</c>.</summary>
    SelfReference,

    /// <summary>The CLI is blocking on user input that will not arrive unattended.</summary>
    NeedsInput,

    /// <summary>A legacy CLI stopped without emitting a terminal completion frame (the silent-completion class).</summary>
    SilentCompletion,
}
