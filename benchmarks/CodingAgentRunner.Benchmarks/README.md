# CodingAgentRunner.Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org) micro-benchmarks of the library's own
hot paths — the work the host does **per line of agent output** while a run streams:

These benchmarks are optional manual runs. They are not part of `dotnet test`, CI
test validation, or the release gate.

- **`AdapterParsingBenchmarks`** — `stream-json` line → typed `CliRunEvent`s, for each
  of Claude / Codex / Gemini, over a representative transcript (session start, text,
  tool call, tool result, terminal completion). This is the hottest path; it runs once
  per output line for the whole length of a run.
- **`UsageParsingBenchmarks`** — `UsageSummaryParser.Parse`, the per-turn usage line →
  token figures the metrics recorder folds in (once per `TurnCompleted`).
- **`QuotaProbingBenchmarks`** — the quota hot paths: Claude usage-endpoint response →
  snapshot, Codex rollout line → snapshot, the Codex probe's full sessions-directory
  scan, the Codex `token_count` adapter path, the free event harvest
  (`QuotaService.Observe`, once per `RateLimitObserved` in a live run), and one
  shared-store read-merge-write round trip. The Claude probe's HTTP latency is out of
  scope — these measure the library's own work.
- **`RenderingBenchmarks`** — the optional `CodingAgentRunner.Rendering` package:
  Markdown → the span/line model, and a line → HTML (once per rendered message in a UI
  consumer; the core event-stream consumer never pays this).

These measure the **library's** overhead and allocation profile, not the agents. They
are deliberately *not* end-to-end model benchmarks — comparing models or wall-clock per
prompt means actually spawning a CLI and burning tokens, which belongs in a separate
harness, not a CI-friendly micro-bench.

The website's end-to-end CLI performance data lives in
`website/data/cli-performance-observations.json`. That file is intentionally separate
from BenchmarkDotNet output: it is for scenario-level CLI observations and links each
scenario back to source tests via `sourceTests`.

## Run

```bash
# all benchmarks (full statistical run — minutes)
dotnet run -c Release --project benchmarks/CodingAgentRunner.Benchmarks -- --filter '*'

# one class
dotnet run -c Release --project benchmarks/CodingAgentRunner.Benchmarks -- --filter '*AdapterParsing*'

# fast smoke check (single invocation, no statistics)
dotnet run -c Release --project benchmarks/CodingAgentRunner.Benchmarks -- --filter '*' --job dry
```

Release config is mandatory — BenchmarkDotNet refuses to run a Debug build. Results
(reports, logs) land in `BenchmarkDotNet.Artifacts/`, which is git-ignored.
