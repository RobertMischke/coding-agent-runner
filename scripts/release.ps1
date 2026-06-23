#requires -Version 7
<#
.SYNOPSIS
  Cut a release: validate, test, tag v<version>, push the tag.
.DESCRIPTION
  The GitHub 'release' workflow then packs and pushes the package to nuget.org
  using the NUGET_API_KEY repository secret. This script uses and stores NO
  secret — it only drives git + tests locally.
.EXAMPLE
  scripts/release.ps1 0.1.0
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+([.-].+)?$') {
  throw "'$Version' is not a semver-ish version (e.g. 0.1.0)"
}

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') { throw "releases are cut from 'main' (you are on '$branch')" }

git diff --quiet;        if ($LASTEXITCODE -ne 0) { throw "working tree is not clean — commit or stash first" }
git diff --cached --quiet; if ($LASTEXITCODE -ne 0) { throw "index is not clean — commit or stash first" }

$tag = "v$Version"
git rev-parse $tag 2>$null; if ($LASTEXITCODE -eq 0) { throw "tag $tag already exists" }

Write-Host "==> Running tests (Release)…"
dotnet test -c Release; if ($LASTEXITCODE -ne 0) { throw "tests failed" }

Write-Host "==> Tagging $tag and pushing…"
git tag -a $tag -m "Release $Version"
git push origin $tag

Write-Host "Done. The 'release' workflow will pack and push $Version to nuget.org."
