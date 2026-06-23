using System.Diagnostics;
using CodingAgentRunner.Model;

namespace CodingAgentRunner.Abstractions;

/// <summary>
/// A spawned child process plus its pipes, as a custom <see cref="ICliProcessSpawner"/>
/// hands it back to the engine. Mirrors what the built-in pipe-redirection spawn
/// produces, so the engine treats a custom spawn (e.g. a Windows pseudo-terminal)
/// identically.
/// </summary>
/// <param name="Process">The started child process.</param>
/// <param name="Stdin">Writable stdin stream (or <see cref="Stream.Null"/> when stdin is denied).</param>
/// <param name="Stdout">Reader for the child's stdout.</param>
/// <param name="Stderr">Reader for the child's stderr (a PTY spawn may merge stderr into stdout and pass a never-emitting reader here).</param>
/// <param name="KillOverride">Optional custom termination (e.g. close the PTY); when null the engine kills the process tree.</param>
public sealed record CliSpawn(
    Process Process,
    Stream Stdin,
    StreamReader Stdout,
    StreamReader Stderr,
    Action<RunStopReason>? KillOverride = null);

/// <summary>
/// Pluggable process spawner. Inject one via <see cref="CliOptions.Spawner"/> to change
/// how the engine launches a CLI — the canonical use is a <b>Windows pseudo-terminal</b>
/// spawner so a Node CLI flushes <c>stdout</c> per newline (block-buffered pipes otherwise
/// hide live output and trip the silence watchdog). When no spawner is set the engine uses
/// plain redirected pipes.
/// <para>The engine has already built the <see cref="ProcessStartInfo"/> (binary, argv,
/// environment hardening, working directory, redirect flags) — the spawner only chooses
/// the launch mechanism.</para>
/// </summary>
public interface ICliProcessSpawner
{
    /// <summary>Launch the prepared <paramref name="startInfo"/> and return the child + pipes.</summary>
    CliSpawn Spawn(ProcessStartInfo startInfo);
}
