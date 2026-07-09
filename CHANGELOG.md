# Changelog

All notable changes to CodingAgentRunner are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/) (pre-1.0: the public API may still shift).

## [0.4.0] - 2026-07-10

### Added

- `CliThinkingLevels.Ultra` (`"ultra"`) — the top Codex reasoning rung, one step
  above `xhigh`. Codex validates this value server-side; a run that requests it now
  passes `-c model_reasoning_effort="ultra"` instead of falling back to the CLI
  config default.
- Recognition of the **gpt-5.6** Codex model family (prefix match on `gpt-5.6`,
  covering `gpt-5.6-sol`, plain `gpt-5.6`, and future variants) as supporting the
  full ladder including `xhigh` and `ultra`. A UI reading `CliCapabilities`
  (New Task dialogs, ladders) now offers those rungs for gpt-5.6 models.
- `CliThinkingLevels.DisplayName(level)` — short human labels for the rungs
  (`"Extra High"`, `"Ultra"`, …); unknown ids are echoed back rather than dropped.

### Unchanged

- Older Codex models keep their existing gating: `gpt-5` and `gpt-5-codex` top out
  at `high`, `gpt-5.5` at `xhigh` (no `ultra`).
- Claude, Gemini, and Antigravity ladders are unaffected; `ultra` is Codex-only and
  never leaks into a Claude ladder (Claude tops out at `max`).

## [0.3.1]

Baseline for this changelog.

[0.4.0]: https://github.com/RobertMischke/coding-agent-runner/releases/tag/v0.4.0
[0.3.1]: https://github.com/RobertMischke/coding-agent-runner/releases/tag/v0.3.1
