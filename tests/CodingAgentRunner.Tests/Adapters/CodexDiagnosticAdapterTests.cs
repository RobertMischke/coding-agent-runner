using CodingAgentRunner.Adapters;
using CodingAgentRunner.Events;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Adapters;

public sealed class CodexDiagnosticAdapterTests
{
    public static IEnumerable<object[]> KnownDiagnostics =>
        new[]
        {
            new object[]
            {
                "2026-06-01T07:39:35.374Z warning [computer-use-native-pipe] computer-use native pipe startup failed errorMessage=\"Windows Computer Use helper paths are unavailable\" platform=win32",
                new Action<CliRunEvent.Diagnostic>(d =>
                {
                    Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                    Assert.Equal("path", d.Subsystem);
                    Assert.Equal("codex.path.helper-binary", d.Code);
                    Assert.Equal("path-helper-binary", d.Category);
                    Assert.Contains("helper binary", d.Summary, StringComparison.OrdinalIgnoreCase);
                    Assert.Equal("codex.path.helper-binary", d.DedupeKey);
                    Assert.Empty(d.Plugins);
                    Assert.Equal(1, d.Count);
                })
            },
            new object[]
            {
                "failed to load plugin: plugin is not installed plugin=\"computer-use@openai-bundled\" path=/Users/macmini/.codex/plugins/cache/openai-bundled/computer-use",
                new Action<CliRunEvent.Diagnostic>(d =>
                {
                    Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
                    Assert.Equal("plugin-loader", d.Subsystem);
                    Assert.Equal("codex.plugin.not-installed", d.Code);
                    Assert.Equal("plugin-not-installed", d.Category);
                    Assert.Contains("computer-use@openai-bundled", d.Summary);
                    Assert.Equal("codex.plugin.not-installed|computer-use@openai-bundled", d.DedupeKey);
                    Assert.Equal(["computer-use@openai-bundled"], d.Plugins);
                    Assert.Equal(1, d.Count);
                })
            },
        };

    [Theory]
    [MemberData(nameof(KnownDiagnostics))]
    public void Stderr_warnings_become_typed_diagnostics(string line, Action<CliRunEvent.Diagnostic> assertDiagnostic)
    {
        var evt = Assert.Single(CodexEventAdapter.Map(line, "run-1", CliStreamKind.Stderr).ToList());
        var diagnostic = Assert.IsType<CliRunEvent.Diagnostic>(evt);
        Assert.Equal("run-1", diagnostic.RunId);
        Assert.Equal(line.Trim(), diagnostic.RawDetail);
        assertDiagnostic(diagnostic);
    }

    [Fact]
    public void Unknown_stderr_keeps_the_full_line_lossless()
    {
        const string line = "2026-06-01T07:39:35.374Z warning [codex] unclassified stderr payload with a long tail";

        var evt = Assert.Single(CodexEventAdapter.Map(line, "run-1", CliStreamKind.Stderr).ToList());
        var unknown = Assert.IsType<CliRunEvent.Unknown>(evt);
        Assert.Equal(line, unknown.RawDetail);
        Assert.StartsWith("2026-06-01T07:39:35.374Z warning", unknown.Sample);
    }

    [Fact]
    public void Structured_session_and_turn_frames_stay_typed()
    {
        var started = Assert.IsType<CliRunEvent.SessionStarted>(
            Assert.Single(CodexEventAdapter.Map("""{"type":"thread.started","thread_id":"t-9"}""", "run-1").ToList()));
        Assert.Equal("t-9", started.SessionId);

        var completed = Assert.IsType<CliRunEvent.TurnCompleted>(
            Assert.Single(CodexEventAdapter.Map("""{"type":"turn.completed","usage":{"input_tokens":12,"output_tokens":34}}""", "run-1").ToList()));
        Assert.Equal("input=12 output=34 cache_read=0", completed.UsageSummary);
    }
}
