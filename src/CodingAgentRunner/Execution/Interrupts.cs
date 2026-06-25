using CodingAgentRunner.Events;

namespace CodingAgentRunner.Execution;

/// <summary>
/// The run context an <see cref="IInterruptClassifier"/> sees alongside a raw line —
/// enough to judge a stop condition without the classifier holding any run state
/// itself. Passed by <c>in</c> so classifying a high-volume stream allocates nothing.
/// </summary>
/// <param name="RunId">The run the line belongs to.</param>
/// <param name="Phase">The lifecycle phase the run is in.</param>
/// <param name="SilenceSeconds">How long the run has been silent when this line arrived.</param>
/// <param name="CliType">The CLI being run (one of <see cref="Model.CliTypes"/>).</param>
public readonly record struct InterruptContext(
    string RunId,
    RunPhase Phase,
    double SilenceSeconds,
    string CliType);

/// <summary>
/// A classifier's verdict that a run hit a stop condition. The library produces it
/// (it owns the error grammar); the host keeps authority over what to do — it stays
/// the owner of <c>Stop()</c>. The engine maps a non-null signal onto a
/// <see cref="CliRunEvent.Interrupt"/> on the event stream via <see cref="ToEvent"/>,
/// so the host reacts to a typed event, never to a raw line.
/// </summary>
/// <param name="Kind">Which stop condition was recognised.</param>
/// <param name="Detail">A short human-readable explanation (goes into the marker / log).</param>
/// <param name="IsFatal">True when the run cannot make progress and should be stopped; false annotates a recognised-but-harmless case (e.g. <see cref="InterruptReason.SelfReference"/>).</param>
public sealed record InterruptSignal(InterruptReason Kind, string Detail, bool IsFatal)
{
    /// <summary>Project this verdict onto the typed event the engine puts on the stream for <paramref name="runId"/>.</summary>
    public CliRunEvent.Interrupt ToEvent(string runId)
        => new(Kind, Detail, IsFatal) { RunId = runId };
}

/// <summary>
/// Recognises stop conditions in a CLI's raw output, one line at a time. The library
/// owns the error grammar (it knows what a sandbox refusal or a quota wall looks
/// like); a consumer never re-implements this. Implementations must be pure and
/// total — any line, including malformed input, yields a verdict or null, never an
/// exception.
/// </summary>
public interface IInterruptClassifier
{
    /// <summary>Judge one raw output line. Returns an <see cref="InterruptSignal"/> when a stop condition is recognised, or null to let the run continue.</summary>
    InterruptSignal? Classify(string rawLine, in InterruptContext context);
}

/// <summary>
/// Built-in <see cref="IInterruptClassifier"/>s and combinators. Descriptors compose
/// these; a CLI that needs nothing special uses <see cref="None"/>.
/// </summary>
public static class InterruptClassifiers
{
    /// <summary>A classifier that never interrupts — the default for a CLI with no special stop grammar.</summary>
    public static IInterruptClassifier None { get; } = new NoneClassifier();

    /// <summary>Wrap a predicate as a classifier.</summary>
    public static IInterruptClassifier Predicate(Func<string, InterruptContext, InterruptSignal?> classify)
        => new PredicateClassifier(classify);

    /// <summary>Run several classifiers in order; the first non-null verdict wins.</summary>
    public static IInterruptClassifier Composite(params IInterruptClassifier[] classifiers)
        => new CompositeClassifier(classifiers);

    /// <summary>
    /// Recognises <see cref="InterruptReason.EnvironmentBlocker"/> lines — runtime / sandbox
    /// / OS failures the agent cannot self-recover from — by case-insensitive substring against
    /// a conservative starter pattern set. The host supplies its full curated grammar via
    /// <see cref="EnvironmentBlocker(IEnumerable{string})"/> when the engine read-loop is wired.
    /// </summary>
    public static IInterruptClassifier EnvironmentBlocker() => EnvironmentBlocker(DefaultBlockerPatterns);

    /// <summary>An environment-blocker classifier matching <paramref name="patterns"/> (case-insensitive substring).</summary>
    public static IInterruptClassifier EnvironmentBlocker(IEnumerable<string> patterns)
        => new EnvironmentBlockerClassifier(patterns);

    // A conservative starter set of unambiguous OS/sandbox refusals. The host's
    // AgentEnvironmentDetector carries the full curated grammar and replaces this
    // when the interrupt step wires the engine read-loop.
    private static readonly string[] DefaultBlockerPatterns =
    [
        "CreateProcessAsUserW failed",
        "EACCES",
        "permission denied",
        "Operation not permitted",
    ];

    private sealed class NoneClassifier : IInterruptClassifier
    {
        public InterruptSignal? Classify(string rawLine, in InterruptContext context) => null;
    }

    private sealed class PredicateClassifier(Func<string, InterruptContext, InterruptSignal?> classify) : IInterruptClassifier
    {
        public InterruptSignal? Classify(string rawLine, in InterruptContext context) => classify(rawLine, context);
    }

    private sealed class CompositeClassifier(IInterruptClassifier[] classifiers) : IInterruptClassifier
    {
        public InterruptSignal? Classify(string rawLine, in InterruptContext context)
        {
            foreach (var c in classifiers)
            {
                var verdict = c.Classify(rawLine, in context);
                if (verdict is not null) return verdict;
            }
            return null;
        }
    }

    private sealed class EnvironmentBlockerClassifier(IEnumerable<string> patterns) : IInterruptClassifier
    {
        private readonly string[] _patterns = patterns.ToArray();

        public InterruptSignal? Classify(string rawLine, in InterruptContext context)
        {
            if (string.IsNullOrEmpty(rawLine)) return null;
            foreach (var p in _patterns)
                if (rawLine.Contains(p, StringComparison.OrdinalIgnoreCase))
                    return new InterruptSignal(InterruptReason.EnvironmentBlocker, p, IsFatal: true);
            return null;
        }
    }
}
