<#
    Mirrors this repo's Mod\Parachute\ into the game's Mods folder.
    Never touches any other mod. Run from anywhere:
        pwsh -File tools\deploy.ps1
#>
param(
    [string]$GamePath = 'C:\7D2D\Custom\v3.4'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'Mod\Parachute'
$target = Join-Path $GamePath 'Mods\Parachute'

if (-not (Test-Path $source)) { throw "Source not found: $source" }
if (-not (Test-Path (Join-Path $GamePath '7DaysToDie.exe'))) { throw "Game not found at: $GamePath" }

Write-Host "Deploying $source -> $target"
robocopy $source $target /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
Write-Host "Done."
