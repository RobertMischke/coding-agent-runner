#!/usr/bin/env bash
# Pack the library locally into ./artifacts (no publish). Optional version arg.
# Usage: scripts/pack.sh [0.1.0-local]
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

version_arg=()
[[ -n "${1:-}" ]] && version_arg=(-p:Version="$1")

dotnet pack src/CodingAgentRunner/CodingAgentRunner.csproj -c Release "${version_arg[@]}" -o artifacts
dotnet pack src/CodingAgentRunner.Rendering/CodingAgentRunner.Rendering.csproj -c Release "${version_arg[@]}" -o artifacts
echo "Packed into $root/artifacts:"
ls -1 artifacts/*.nupkg 2>/dev/null || true
