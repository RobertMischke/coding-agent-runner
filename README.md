# CodingAgentRunner

> Run coding-agent CLIs — Claude Code, Codex, Copilot, Gemini — from .NET: hardened spawning, streamed events, lifecycle & quota tracking.

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

**CodingAgentRunner** is a .NET library for launching and supervising terminal-native coding agents (Claude Code, OpenAI Codex, GitHub Copilot CLI, Gemini CLI) as child processes — reliably, especially on Windows.

It is the "boring but critical" infrastructure layer: it spawns the agent CLI with the right binary, environment, and isolation; reads its `stream-json` output as a structured event stream; manages the process lifecycle (stop, reap, watchdog); enforces a *platform-owns-git* boundary; and tracks remaining quota with a smart cache. Think of it as one level above [CliWrap](https://github.com/Tyrrrz/CliWrap): not "run any process", but "run a *coding agent* and understand it".

> **Status: early / work in progress.** Extracted and generalized from a production multi-agent orchestrator. The public API is still settling — pin a version and watch releases.

## Why

Running these CLIs from another process on Windows is full of footguns that each cost real debugging time. CodingAgentRunner encodes the fixes so you don't have to rediscover them:

- **The `.cmd`-shim prompt truncation.** On Windows `claude`/`codex` often resolve to a `.cmd` shim; spawning it routes through `cmd.exe`, which silently truncates a multi-line prompt at the first newline — so the agent receives only the first line and no task. The runner resolves and launches the real `.exe`.
- **Platform-owns-git boundary.** A guard keeps the agent from running `git commit`/`push` (the host owns version control) — which also avoids a class of mid-run crashes.
- **Clean-context isolation.** Each run gets an isolated CLI home so concurrent runs (and your own interactive session) don't collide.
- **Honest completion detection.** A stream-aware sentinel scanner tells the agent's *own* `[[TASK_DONE]]` apart from one it merely *read* in a file.
- **Quota awareness.** Probe and cache the remaining rate-limit window per CLI, with an escalation policy that polls more often as you approach the limit.

See [docs/why-windows-hardening.md](docs/why-windows-hardening.md) for the full stories behind each.

## Supported agents

Claude Code · OpenAI Codex · GitHub Copilot CLI · Gemini CLI (extensible).

## Features

- ✅ Stream-aware terminal-sentinel detection (`[[TASK_DONE]]` / `BLOCKED` / `NEEDS_INPUT` / `NOOP`).
- 🚧 Hardened process spawning (binary resolution, environment hardening, clean-context).
- 🚧 `stream-json` → structured event stream.
- 🚧 Lifecycle: stop, process-tree reap, watchdog.
- 🚧 Platform-owns-git guard.
- 🚧 Quota probing + smart escalation caching.

(✅ landed · 🚧 migrating from the source orchestrator)

## Quickstart

> The public API is still being shaped during extraction. A usage example will land as the surface stabilizes — see [docs/architecture.md](docs/architecture.md) for the planned shape.

## Project layout

```
src/CodingAgentRunner/          the library
  Sentinels/                    terminal-sentinel detection (landed)
tests/CodingAgentRunner.Tests/  xUnit tests
docs/                           developer wiki (architecture, the "why")
website/                        project website (static, English)
```

## Build & test

```bash
dotnet build
dotnet test
```

Requires the .NET 8 SDK.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This is an active extraction — issues and PRs welcome.

## License

[Apache-2.0](LICENSE) © 2026 Robert Mischke.
