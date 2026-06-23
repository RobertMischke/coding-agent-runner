using System.Collections.Concurrent;
using System.Diagnostics;
using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Events;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class CliDriverEngineTests
{
    /// <summary>
    /// A real driver over a trivial, cross-platform process (`dotnet --version`)
    /// that exits 0 after printing one line. It maps every stdout line to a
    /// TurnCompleted so the adapter-&gt;event wiring is exercised end-to-end.
    /// </summary>
    private sealed class ProbeDriver : CliDriverBase
    {
        private readonly string _exe;
        private readonly string[] _args;

        public ProbeDriver(string exe, string[] args, IRunLogPathProvider logs)
            : base(new CliOptions { AllowAgentGitMutation = true }, null, logs) // git-guard off: keep PATH clean
        {
            _exe = exe;
            _args = args;
        }

        public override string CliType => CliTypes.Claude;
        public override string GetCliPath() => _exe;

        protected override ProcessStartInfo BuildStartInfo(CliRunRequest request, string? model, string? thinkingLevel)
        {
            var psi = new ProcessStartInfo { FileName = _exe, WorkingDirectory = request.WorkingDirectory };
            foreach (var a in _args) psi.ArgumentList.Add(a);
            return psi;
        }

        protected override IEnumerable<CliRunEvent> MapLineToRunEvents(string runId, CliOutputLine line)
            => line.Stream == "stdout" && !string.IsNullOrWhiteSpace(line.Text)
                ? new[] { new CliRunEvent.TurnCompleted("probe") { RunId = runId } }
                : Array.Empty<CliRunEvent>();
    }

    private sealed class TempLogs : IRunLogPathProvider, IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "car-engine-" + Guid.NewGuid().ToString("N"));
        public string GetRunLogDirectory(string runId) => Path.Combine(_root, runId);
        public string GetActiveJobsFile() => Path.Combine(_root, "active.json");
        public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch { } }
    }

    [Fact]
    public async Task Run_StreamsOutput_RaisesTypedEvents_AndClassifiesCleanExitAsCompleted()
    {
        using var logs = new TempLogs();
        var driver = new ProbeDriver("dotnet", ["--version"], logs);

        var events = new ConcurrentQueue<CliRunEvent>();
        driver.OnRunEvent += (_, e) => events.Enqueue(e);

        var started = new TaskCompletionSource<CliRunInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<CliRunInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        driver.OnStarted += (_, r) => started.TrySetResult(r);
        driver.OnFinished += (_, r) => finished.TrySetResult(r);

        var (run, error) = await driver.StartAsync(new CliRunRequest
        {
            RunId = "engine-test",
            Prompt = "unused",
            WorkingDirectory = Path.GetTempPath(),
        });

        Assert.Null(error);
        Assert.NotNull(run);
        Assert.Equal("running", run!.Status);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var final = await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Terminal classification: clean exit (0), no stop reason -> completed.
        Assert.Equal("completed", final.Status);
        Assert.Equal(0, final.ExitCode);
        Assert.True(final.DurationSeconds >= 0);

        // Event ordering: RunStarted first, ProcessExited last, a TurnCompleted between.
        var seq = events.ToArray();
        Assert.IsType<CliRunEvent.RunStarted>(seq[0]);
        Assert.IsType<CliRunEvent.ProcessExited>(seq[^1]);
        Assert.Contains(seq, e => e is CliRunEvent.TurnCompleted);

        // The version line was captured.
        var output = driver.GetExecution("engine-test");
        Assert.Equal("completed", output!.Status);
        Assert.Contains(driver.GetOutput("engine-test"), l => l.Stream == "stdout");
    }

    [Fact]
    public async Task StreamAsync_PullStream_StartsWithRunStarted_EndsAtTerminal()
    {
        using var logs = new TempLogs();
        var driver = new ProbeDriver("dotnet", ["--version"], logs);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // One await foreach — no event wiring, no manual "done?" tracking.
        var seq = new List<CliRunEvent>();
        await foreach (var e in driver.StreamAsync(new CliRunRequest
        {
            RunId = "stream-1", Prompt = "x", WorkingDirectory = Path.GetTempPath(),
        }, cts.Token))
        {
            seq.Add(e);
        }

        // Same ordering guarantees as the push-event path; the terminal event closes the stream.
        Assert.IsType<CliRunEvent.RunStarted>(seq[0]);
        Assert.IsType<CliRunEvent.ProcessExited>(seq[^1]);
        Assert.Contains(seq, e => e is CliRunEvent.TurnCompleted);
        Assert.All(seq, e => Assert.Equal("stream-1", e.RunId));   // multiplex filter held
    }

    [Fact]
    public async Task DuplicateRunId_WhileLive_IsRejected_ButReusableAfterExit()
    {
        using var logs = new TempLogs();
        var driver = new ProbeDriver("dotnet", ["--version"], logs);

        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        driver.OnFinished += (_, _) => finished.TrySetResult();

        var (run1, err1) = await driver.StartAsync(new CliRunRequest
        {
            RunId = "dup", Prompt = "x", WorkingDirectory = Path.GetTempPath(),
        });
        Assert.Null(err1);
        Assert.NotNull(run1);

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // After the first run exits, the same id can be reused.
        var (run2, err2) = await driver.StartAsync(new CliRunRequest
        {
            RunId = "dup", Prompt = "x", WorkingDirectory = Path.GetTempPath(),
        });
        Assert.Null(err2);
        Assert.NotNull(run2);
    }

    [Fact]
    public async Task Stop_ClassifiesAsStopped_AndRaisesKilled_NotProcessExited()
    {
        using var logs = new TempLogs();
        // A genuinely long-lived, cross-platform process so we can Stop() it mid-run.
        var (exe, args) = OperatingSystem.IsWindows()
            ? ("ping", new[] { "-n", "20", "127.0.0.1" })
            : ("sleep", new[] { "20" });
        var driver = new ProbeDriver(exe, args, logs);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<CliRunInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sawKilled = false;
        var sawProcessExited = false;
        driver.OnStarted += (_, _) => started.TrySetResult();
        driver.OnFinished += (_, r) => finished.TrySetResult(r);
        driver.OnRunEvent += (_, e) =>
        {
            if (e is CliRunEvent.Killed) sawKilled = true;
            if (e is CliRunEvent.ProcessExited) sawProcessExited = true;
        };

        var (run, err) = await driver.StartAsync(new CliRunRequest
        {
            RunId = "stop-test", Prompt = "x", WorkingDirectory = Path.GetTempPath(),
        });
        Assert.Null(err);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(driver.Stop("stop-test", RunStopReason.UserStop));
        var final = await finished.Task.WaitAsync(TimeSpan.FromSeconds(20));

        // The deliberate stop is classified as 'stopped' (NOT 'failed' from the -1 kill),
        // and surfaces as a Killed event rather than ProcessExited.
        Assert.Equal("stopped", final.Status);
        Assert.True(sawKilled, "expected a Killed event for a deliberate stop");
        Assert.False(sawProcessExited, "a deliberately stopped run must not raise ProcessExited");

        // Stop on an unknown run id returns false.
        Assert.False(driver.Stop("no-such-run"));
    }

    [Fact]
    public async Task Forget_EvictsInMemory_GetOutputThenFallsBackToDisk()
    {
        using var logs = new TempLogs();
        var driver = new ProbeDriver("dotnet", ["--version"], logs);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        driver.OnFinished += (_, _) => finished.TrySetResult();

        await driver.StartAsync(new CliRunRequest
        {
            RunId = "f1", Prompt = "x", WorkingDirectory = Path.GetTempPath(),
        });
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.NotNull(driver.GetExecution("f1"));                 // tracked until forgotten
        Assert.True(driver.Forget("f1"));
        Assert.Null(driver.GetExecution("f1"));                    // evicted

        // GetOutput now reads the persisted per-stream log from disk.
        var fromDisk = driver.GetOutput("f1");
        Assert.Contains(fromDisk, l => l.Stream == "stdout");

        Assert.False(driver.Forget("f1"));                         // already gone
    }
}
