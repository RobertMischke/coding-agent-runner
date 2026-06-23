namespace CodingAgentRunner.Sentinels;

/// <summary>
/// Live-stream terminal-sentinel detector. Decides, from the CLI output captured
/// SO FAR (mid-run), whether the agent has emitted its OWN terminal sentinel
/// (<c>[[TASK_DONE]]</c> / <c>[[TASK_BLOCKED]]</c> / …) so a lingering process can
/// be reaped without waiting for the CLI to exit on its own.
///
/// <para>
/// <b>Why this is a separate, tested helper.</b> The naive version
/// (<see cref="AgentSentinel.Regex"/><c>.IsMatch</c> on every raw output line)
/// caused a dominant "tasks never complete" incident: agent rules, contract docs,
/// and runner source are FULL of <c>[[TASK_DONE]]</c> literals, so any run that
/// merely READ such a file (file content rides the <c>user</c> / tool-result
/// stream) tripped the scanner and was killed mid-work as a false "completion".
/// </para>
///
/// <para>
/// Two guards keep it honest: (1) only the AGENT's own stream can carry a terminal
/// sentinel — <c>system</c> / <c>user</c> (tool-result) / <c>orchestrator</c> /
/// <c>stderr</c> lines are dropped; (2) only a STANDALONE sentinel line counts —
/// the token (with its optional reason) is essentially the whole line, modulo a
/// little markdown/quote decoration — so a sentinel mentioned inside prose or
/// quoted code does not fire. Missing a real terminal sentinel is harmless (the
/// run finalizes when the CLI exits); a false positive kills live work, so this
/// errs toward not stopping.
/// </para>
/// </summary>
public static class LiveSentinelScanner
{
    /// <summary>
    /// Decoration slack: characters of <c>** &gt; - `</c> markdown/quote that may
    /// wrap a standalone sentinel line beyond the matched token itself.
    /// </summary>
    private const int DecorationSlack = 8;

    /// <summary>
    /// Returns <c>true</c> when the most recent non-empty agent-stream line is a
    /// standalone terminal sentinel.
    /// </summary>
    public static bool HasStandaloneAgentSentinel(IReadOnlyList<CliOutputLine>? snapshot)
    {
        if (snapshot is null || snapshot.Count == 0) return false;

        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            var line = snapshot[i];
            var stream = line?.Stream ?? string.Empty;

            // Mirror the post-run agent-text join: only the agent's own stream
            // can carry a terminal sentinel.
            if (string.Equals(stream, "system", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(stream, "user", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(stream, "orchestrator", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(stream, "stderr", StringComparison.OrdinalIgnoreCase)) continue;

            var text = (line?.Text ?? string.Empty).Trim();
            if (text.Length == 0) continue;

            var match = AgentSentinel.Regex.Match(text);
            if (match.Success && text.Length <= match.Length + DecorationSlack) return true;
        }

        return false;
    }
}
