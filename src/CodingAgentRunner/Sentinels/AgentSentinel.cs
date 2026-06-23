using System.Text.RegularExpressions;

namespace CodingAgentRunner.Sentinels;

/// <summary>
/// The terminal-sentinel contract a coding agent uses to signal how its run
/// ended on its final line:
/// <list type="bullet">
///   <item><c>[[TASK_DONE]]</c></item>
///   <item><c>[[TASK_BLOCKED:&lt;reason&gt;]]</c></item>
///   <item><c>[[TASK_NEEDS_INPUT:&lt;reason&gt;]]</c></item>
///   <item><c>[[TASK_NOOP]]</c></item>
/// </list>
/// </summary>
public static class AgentSentinel
{
    /// <summary>
    /// Matches a double-bracket terminal sentinel anywhere in a line, capturing
    /// the <c>keyword</c> and optional <c>reason</c>. Kept intentionally strict
    /// (double brackets required) so it never fires on ordinary prose. For the
    /// live, stream-aware, standalone-line decision use
    /// <see cref="LiveSentinelScanner.HasStandaloneAgentSentinel"/>.
    /// </summary>
    public static readonly Regex Regex = new(
        @"\[\[\s*TASK[\s_-]*(?<keyword>DONE|BLOCKED|NEEDS[\s_-]*INPUT|NOOP)\s*(?::\s*(?<reason>[^\]]*?))?\s*\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
