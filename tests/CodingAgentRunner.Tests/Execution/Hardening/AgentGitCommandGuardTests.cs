using System.Diagnostics;
using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Execution.Hardening;
using Xunit;

namespace CodingAgentRunner.Tests.Execution.Hardening;

public class AgentGitCommandGuardTests
{
    [Theory]
    [InlineData(new[] { "commit", "-m", "x" }, true)]
    [InlineData(new[] { "push" }, true)]
    [InlineData(new[] { "-C", "/repo", "push" }, true)]          // skips the -C value
    [InlineData(new[] { "-c", "user.name=x", "commit" }, true)] // skips the -c value
    [InlineData(new[] { "status" }, false)]
    [InlineData(new[] { "-c", "core.pager=cat", "log" }, false)]
    [InlineData(new[] { "diff" }, false)]
    public void IsForbidden_DetectsMutatingCommandPastGlobalOptions(string[] args, bool expected)
    {
        Assert.Equal(expected, AgentGitCommandGuard.IsForbidden(args, new GitGuardOptions()));
    }

    [Fact]
    public void Apply_GeneratesBrandNeutralWrappers_WithCustomPrefix()
    {
        var opts = new GitGuardOptions
        {
            EnvPrefix = "MYTOOL",
            GuardDirName = "coding-agent-runner-test-guard-" + Guid.NewGuid().ToString("N"),
            BlockMessage = "mytool: agents must not run git {cmd}."
        };

        var psi = new ProcessStartInfo();
        AgentGitCommandGuard.Apply(psi, opts);

        var dir = Path.Combine(Path.GetTempPath(), opts.GuardDirName);
        if (!Directory.Exists(dir))
            return; // git not on PATH in this environment -> guard no-ops; nothing to assert

        try
        {
            var posix = File.ReadAllText(Path.Combine(dir, "git"));

            // Uses the custom prefix, carries the custom message, names a forbidden cmd...
            Assert.Contains("MYTOOL_ALLOW_GIT_MUTATION", posix);
            Assert.Contains("mytool: agents must not run git", posix);
            Assert.Contains("commit", posix);
            Assert.Contains("exit 86", posix);

            // ...and is brand-neutral (no leakage from the source application).
            Assert.DoesNotContain("AGENT_TASKBOARD", posix);
            Assert.DoesNotContain("agent-taskboard", posix);

            // The guard injected its directory at the FRONT of PATH.
            Assert.StartsWith(dir, psi.Environment["PATH"]);
            Assert.Equal(dir, psi.Environment["MYTOOL_GUARD_DIR"]);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Apply_NoOps_WhenMutationAllowed()
    {
        var psi = new ProcessStartInfo();
        AgentGitCommandGuard.Apply(psi, new GitGuardOptions(), allowMutation: true);
        Assert.False(psi.Environment.ContainsKey("CODING_AGENT_RUNNER_GUARD_DIR"));
    }
}
