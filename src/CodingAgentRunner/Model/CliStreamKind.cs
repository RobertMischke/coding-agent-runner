namespace CodingAgentRunner.Model;

/// <summary>
/// Which standard stream a raw CLI output line came from. Replaces the loose
/// string tag on <see cref="CliOutputLine.Stream"/> at the parsing boundary so a
/// <see cref="Execution.CliParser"/> can switch on a closed set instead of
/// comparing magic strings.
/// </summary>
public enum CliStreamKind
{
    /// <summary>The process's standard output — where every structured CLI protocol frame arrives.</summary>
    Stdout,

    /// <summary>The process's standard error — diagnostics and the occasional environment-blocker line.</summary>
    Stderr,

    /// <summary>A synthetic line the runner itself injected (markers, notices), not produced by the CLI.</summary>
    System,
}

/// <summary>Helpers for <see cref="CliStreamKind"/>.</summary>
public static class CliStreamKinds
{
    /// <summary>
    /// Map a raw stream name (as carried on <see cref="CliOutputLine.Stream"/>) onto the
    /// enum. Unknown / empty names fall back to <see cref="CliStreamKind.Stdout"/> — the
    /// stream a parser must always be willing to read.
    /// </summary>
    public static CliStreamKind Parse(string? stream) => (stream ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "stderr" => CliStreamKind.Stderr,
        "system" => CliStreamKind.System,
        _ => CliStreamKind.Stdout,
    };

    /// <summary>The canonical lowercase stream name for a kind (round-trips with <see cref="Parse"/>).</summary>
    public static string Name(this CliStreamKind kind) => kind switch
    {
        CliStreamKind.Stderr => "stderr",
        CliStreamKind.System => "system",
        _ => "stdout",
    };
}
