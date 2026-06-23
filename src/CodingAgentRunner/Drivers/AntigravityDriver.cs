using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Adapters;
using CodingAgentRunner.Events;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Execution.Hardening;
using CodingAgentRunner.Model;

namespace CodingAgentRunner.Drivers;

/// <summary>
/// Google Antigravity driver (the <c>agentapi</c> CLI) — the maintained Google
/// integration that supersedes the deprecated Gemini CLI.
/// <list type="bullet">
///   <item>New conversation: <c>agentapi new-conversation [--model=flash|pro|flash_lite] "&lt;prompt&gt;"</c></item>
///   <item>Resume: <c>agentapi send-message &lt;conversationId&gt; "&lt;prompt&gt;"</c></item>
/// </list>
/// <para>
/// The prompt is the last positional argv. Typed events reuse the Gemini frame adapter;
/// Antigravity's richer frame-to-marker rendering is a consumer concern (an output
/// renderer), not the driver's. Shared-only (no clean-context).
/// </para>
/// </summary>
internal sealed class AntigravityDriver : CliDriverBase
{
    private static readonly Regex Uuid =
        new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    /// <summary>Create an Antigravity driver.</summary>
    public AntigravityDriver(
        CliOptions? options = null,
        ILogger? logger = null,
        IRunLogPathProvider? logPaths = null,
        IUserHomeProvider? home = null)
        : base(options, logger, logPaths, home) { }

    /// <inheritdoc />
    public override string CliType => CliTypes.Antigravity;

    /// <inheritdoc />
    public override string GetCliPath() => Options.AntigravityPath ?? "agentapi";

    /// <summary>Antigravity resumes a conversation via <c>send-message &lt;id&gt;</c>.</summary>
    protected override bool SupportsResume => true;

    /// <inheritdoc />
    protected override ProcessStartInfo BuildStartInfo(CliRunRequest request, string? model, string? thinkingLevel)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SafeResolve(GetCliPath()),
            WorkingDirectory = request.WorkingDirectory,
        };

        if (!string.IsNullOrWhiteSpace(request.ResumeSessionId))
        {
            psi.ArgumentList.Add("send-message");
            psi.ArgumentList.Add(request.ResumeSessionId!);
        }
        else
        {
            psi.ArgumentList.Add("new-conversation");
            var mapped = MapModel(model);
            if (!string.IsNullOrEmpty(mapped))
                psi.ArgumentList.Add($"--model={mapped}");
        }

        // The prompt is the last positional argument (a space when empty, so argv stays well-formed).
        psi.ArgumentList.Add(string.IsNullOrEmpty(request.Prompt) ? " " : request.Prompt);
        return psi;
    }

    /// <summary>Map a free model id onto Antigravity's three tiers.</summary>
    private static string? MapModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;
        var m = model.ToLowerInvariant();
        if (m.Contains("lite")) return "flash_lite";
        if (m.Contains("pro")) return "pro";
        if (m.Contains("flash")) return "flash";
        return "flash";
    }

    /// <summary>Antigravity takes the prompt as argv, not stdin.</summary>
    protected override string? GetPromptStdinPayload(CliRunRequest request, string? model) => null;

    /// <inheritdoc />
    protected override IEnumerable<CliRunEvent> MapLineToRunEvents(string runId, CliOutputLine line)
        => line.Stream == "stdout" ? GeminiEventAdapter.Map(line.Text, runId) : Array.Empty<CliRunEvent>();

    /// <summary>Antigravity resumes a conversation UUID; a slug from another CLI is rejected.</summary>
    public override bool IsCompatibleSessionId(string? sessionId)
        => !string.IsNullOrWhiteSpace(sessionId) && Uuid.IsMatch(sessionId);

    /// <summary>
    /// Availability probe. <c>agentapi</c> has no <c>--version</c>, so a non-zero exit
    /// that reports an unknown command (or prints its usage banner) still means the CLI
    /// is present.
    /// </summary>
    public override (bool Available, string? Version, string Path) TestCliPath(string? path = null)
    {
        var resolved = SafeResolve(string.IsNullOrWhiteSpace(path) ? GetCliPath() : path.Trim());
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = resolved,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var o = proc.StandardOutput.ReadToEnd().Trim();
            var e = proc.StandardError.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            var ok = proc.ExitCode == 0
                || o.Contains("unknown command: --version") || e.Contains("unknown command: --version")
                || o.Contains("Usage: agentapi") || e.Contains("Usage: agentapi");
            return (ok, "agentapi", resolved);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "agentapi not available at '{Path}'", resolved);
            return (false, null, resolved);
        }
    }

    private string SafeResolve(string path)
    {
        try { return BinaryResolver.ResolveExecutable(path); }
        catch { return path; }
    }
}
