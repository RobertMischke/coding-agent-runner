using CodingAgentRunner.Execution;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class NpmShimHealerTests
{
    [Fact]
    public async Task NonWindows_IsANoOp_ReportingAvailable()
    {
        // The healer's failure mode is Windows-specific; everywhere else it must be
        // an immediate, side-effect-free no-op that reports the CLI as available.
        if (OperatingSystem.IsWindows()) return;

        var outcome = await NpmShimHealer.TryHealClaudeAsync();
        Assert.True(outcome.Available);
        Assert.Empty(outcome.Actions);
        Assert.Null(outcome.Error);
    }
}
