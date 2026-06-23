#requires -Version 7
<#
.SYNOPSIS
  Pack the library locally into ./artifacts (no publish). Optional -Version.
.EXAMPLE
  scripts/pack.ps1 -Version 0.1.0-local
#>
[CmdletBinding()]
param([string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$packArgs = @('pack', 'src/CodingAgentRunner/CodingAgentRunner.csproj', '-c', 'Release', '-o', 'artifacts')
if ($Version) { $packArgs += "-p:Version=$Version" }

dotnet @packArgs
if ($LASTEXITCODE -ne 0) { throw "pack failed" }
Get-ChildItem artifacts/*.nupkg | Select-Object -ExpandProperty Name
