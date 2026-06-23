using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Model;

public class RunStatusClassifierTests
{
    [Theory]
    // No stop reason → the exit code decides.
    [InlineData(0, RunStopReason.None, RunOutcome.Completed)]
    [InlineData(1, RunStopReason.None, RunOutcome.Failed)]
    [InlineData(-1, RunStopReason.None, RunOutcome.Failed)]   // Process.Kill / TerminateProcess self-crash signature
    [InlineData(137, RunStopReason.None, RunOutcome.Failed)]
    [InlineData(null, RunStopReason.None, RunOutcome.Failed)]
    // A stop reason → always 'stopped', regardless of exit code.
    [InlineData(0, RunStopReason.UserStop, RunOutcome.Stopped)]
    [InlineData(-1, RunStopReason.Watchdog, RunOutcome.Stopped)]
    [InlineData(1, RunStopReason.Cancelled, RunOutcome.Stopped)]
    [InlineData(0, RunStopReason.QuotaCapExceeded, RunOutcome.Stopped)]
    // ...except the two "the work is done, only a lingering process was killed" reasons,
    // which map to Completed even with a non-zero kill-induced exit code.
    [InlineData(-1, RunStopReason.SentinelDetected, RunOutcome.Completed)]
    [InlineData(-1, RunStopReason.SilentCompletion, RunOutcome.Completed)]
    public void Classify_DistinguishesStoppedFromCompletedAndFailed(
        int? exitCode, RunStopReason reason, RunOutcome expected)
    {
        Assert.Equal(expected, RunStatusClassifier.Classify(exitCode, reason));
    }
}
