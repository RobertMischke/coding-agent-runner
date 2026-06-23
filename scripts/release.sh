#!/usr/bin/env bash
# Cut a release: validate, test, tag v<version>, push the tag.
#
# The GitHub 'release' workflow then packs and pushes the package to nuget.org
# using the NUGET_API_KEY repository secret. This script uses and stores NO
# secret — it only drives git + tests locally.
#
# Usage: scripts/release.sh 0.1.0
set -euo pipefail

version="${1:-}"
if [[ -z "$version" ]]; then
  echo "usage: scripts/release.sh <version>   e.g. scripts/release.sh 0.1.0" >&2
  exit 2
fi
if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-].+)?$ ]]; then
  echo "error: '$version' is not a semver-ish version (e.g. 0.1.0)" >&2
  exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

branch="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$branch" != "main" ]]; then
  echo "error: releases are cut from 'main' (you are on '$branch')" >&2
  exit 1
fi
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "error: working tree is not clean — commit or stash first" >&2
  exit 1
fi

tag="v$version"
if git rev-parse "$tag" >/dev/null 2>&1; then
  echo "error: tag $tag already exists" >&2
  exit 1
fi

echo "==> Running tests (Release)…"
dotnet test -c Release

echo "==> Tagging $tag and pushing…"
git tag -a "$tag" -m "Release $version"
git push origin "$tag"

echo "Done. The 'release' workflow will pack and push $version to nuget.org."
