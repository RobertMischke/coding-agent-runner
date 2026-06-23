using System.Collections.Concurrent;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;

namespace CodingAgentRunner.Events;

/// <summary>
/// The built-in, eingebaut watchdog: attach it to a driver in one line and it tracks
/// every live run's phase and silence for you — no manual phase machine, no silence
/// timestamps, no timer loop in your code.
///
/// <para>
/// It owns the phase state (<see cref="RunPhaseTransitions"/>) and a single internal
/// timer; per run it advances the phase from the event stream, resets the silence
/// clock on activity signals, and evaluates the <see cref="WatchdogPolicy"/> on each
/// tick. <see cref="OnStateChanged"/> and <see cref="OnHung"/> fire <b>once per
/// transition</b> (entering Hung fires OnHung exactly once, not every tick). With
/// <c>autoStop</c> it stops a hung run itself (reported as
/// <see cref="RunStopReason.Watchdog"/> — a deliberate stop, never a crash); without
/// it, you just listen and decide. Multiplex-safe: one watchdog covers all of a
/// driver's concurrent runs. <b>Dispose to detach</b> (otherwise the timer and the
/// driver subscriptions live on).
/// </para>
/// </summary>
public sealed class RunWatchdog : IDisposable
{
    // Each RunState instance is its own lock: Tick reads its fields while HandleEvent
    // writes them, so every access is taken under `lock (st)`.
    private sealed class RunState
    {
        public RunPhase Phase = RunPhase.Spawning;
        public DateTime StartedAt;
        public DateTime LastActivity;
        public WatchdogState Last = WatchdogState.Healthy;
    }

    private readonly ICliDriver _driver;
    private readonly WatchdogPolicy _policy;
    private readonly bool _autoStop;
    private readonly ConcurrentDictionary<string, RunState> _runs = new();
    private readonly Timer _timer;
    private int _disposed;

    /// <summary>Raised once when a run's <see cref="WatchdogState"/> changes (Healthy → Quiet → Suspicious → Hung).</summary>
    public event Action<string, RunPhase, WatchdogState>? OnStateChanged;

    /// <summary>Raised <b>once</b> when a run first becomes <see cref="WatchdogState.Hung"/>: (runId, phase, silenceSeconds).</summary>
    public event Action<string, RunPhase, double>? OnHung;

    private RunWatchdog(ICliDriver driver, WatchdogPolicy policy, bool autoStop)
    {
        _driver = driver;
        _policy = policy;
        _autoStop = autoStop;

        driver.OnStarted += HandleStarted;
        driver.OnRunEvent += HandleEvent;
        driver.OnFinished += HandleFinished;

        var period = TimeSpan.FromSeconds(Math.Max(1, _policy.TickSeconds));
        _timer = new Timer(_ => Tick(), null, period, period);
    }

    /// <summary>Attach a watchdog to a driver. Dispose it to detach.</summary>
    /// <param name="driver">The driver whose runs to supervise.</param>
    /// <param name="policy">Budgets + thresholds; defaults to <see cref="WatchdogPolicy.Default"/>.</param>
    /// <param name="autoStop">When true, a hung run is stopped automatically (reason = Watchdog).</param>
    public static RunWatchdog Attach(ICliDriver driver, WatchdogPolicy? policy = null, bool autoStop = false)
        => new(driver, policy ?? WatchdogPolicy.Default, autoStop);

    /// <summary>The current phase the watchdog believes <paramref name="runId"/> is in, or null when untracked.</summary>
    public RunPhase? PhaseOf(string runId)
    {
        if (!_runs.TryGetValue(runId, out var st)) return null;
        lock (st) return st.Phase;
    }

    private void HandleStarted(string runId, CliRunInfo info)
    {
        var now = DateTime.UtcNow;
        _runs[runId] = new RunState
        {
            Phase = RunPhase.Spawning,
            StartedAt = info.StartedAt == default ? now : info.StartedAt,
            LastActivity = now,
        };
    }

    private void HandleEvent(string runId, CliRunEvent evt)
    {
        if (!_runs.TryGetValue(runId, out var st)) return;
        lock (st)
        {
            st.Phase = RunPhaseTransitions.Apply(st.Phase, evt);
            if (RunPhaseTransitions.IsActivitySignal(evt))
                st.LastActivity = DateTime.UtcNow;
        }
    }

    private void HandleFinished(string runId, CliRunInfo info) => _runs.TryRemove(runId, out _);

    private void Tick()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        var now = DateTime.UtcNow;
        foreach (var (runId, st) in _runs)
        {
            // Snapshot + transition test under the run's lock so an overlapping tick or a
            // concurrent HandleEvent cannot tear the read or double-fire the transition.
            RunPhase phase;
            double silence;
            WatchdogState state;
            bool transitioned;
            lock (st)
            {
                phase = st.Phase;
                silence = (now - st.LastActivity).TotalSeconds;
                var age = (now - st.StartedAt).TotalSeconds;
                state = _policy.Decide(phase, silence, age);
                transitioned = state != st.Last;
                if (transitioned) st.Last = state;
            }
            if (!transitioned) continue;                       // act ONLY on a state change

            OnStateChanged?.Invoke(runId, phase, state);       // callbacks outside the lock
            if (state == WatchdogState.Hung)
            {
                OnHung?.Invoke(runId, phase, silence);         // one-shot: only on entering Hung
                if (_autoStop && _runs.ContainsKey(runId))
                    _driver.Stop(runId, RunStopReason.Watchdog);
            }
        }
    }

    /// <summary>Detach from the driver and stop the internal timer.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _timer.Dispose();                                      // stop new ticks BEFORE detaching
        _driver.OnStarted -= HandleStarted;
        _driver.OnRunEvent -= HandleEvent;
        _driver.OnFinished -= HandleFinished;
        _runs.Clear();
    }
}
