namespace CodingAgentRunner.Sentinels;

/// <summary>
/// One captured line of CLI output, tagged with the stream it came from
/// (e.g. <c>assistant</c>, <c>system</c>, <c>user</c> / tool-result,
/// <c>orchestrator</c>, <c>stderr</c>).
///
/// <para>The stream tag is what lets <see cref="LiveSentinelScanner"/> tell the
/// agent's OWN words apart from file content it merely read (tool results ride
/// the <c>user</c> stream).</para>
/// </summary>
public sealed record CliOutputLine
{
    /// <summary>The stream this line came from.</summary>
    public string? Stream { get; init; }

    /// <summary>The raw text of the line.</summary>
    public string? Text { get; init; }
}
