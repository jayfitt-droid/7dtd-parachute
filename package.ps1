# Builds the mod and packs it for players: dist\Parachute-<version>.zip
# contains one top-level folder, Parachute, so unzipping it into the game's
# Mods folder puts every file where the game expects it.
#   powershell -ExecutionPolicy Bypass -File package.ps1
# The art (asset bundle and icon) is taken as it is in Mod\Parachute; rebuild
# it in Unity first if the model, textures or sound changed (see README.md).
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$modSource = Join-Path $repoRoot 'Mod\Parachute'
$project = Join-Path $repoRoot 'src\Parachute\Parachute.csproj'
$distDir = Join-Path $repoRoot 'dist'

# Compile. The build copies Parachute.dll into Mod\Parachute. Warnings count
# as a failure: the build is kept at 0 warnings. --no-incremental always
# recompiles: an up-to-date build writes nothing, which would fail the
# freshness check below and would also hide warnings from the earlier build.
$buildStart = Get-Date
$buildOutput = & dotnet build $project -c Release -nologo --no-incremental 2>&1
if ($LASTEXITCODE -ne 0) { $buildOutput | Select-Object -Last 20; throw 'Build failed.' }
if (-not ($buildOutput | Select-String -SimpleMatch '0 Warning(s)' -Quiet)) {
    $buildOutput | Select-String -Pattern 'warning' | Select-Object -First 20
    throw 'Build has warnings.'
}

# Everything a player needs must be present before packing.
$required = @(
    'ModInfo.xml',
    'Parachute.dll',
    'Config\item_modifiers.xml', 'Config\items.xml', 'Config\recipes.xml', 'Config\loot.xml',
    'Config\traders.xml', 'Config\buffs.xml', 'Config\sounds.xml', 'Config\Localization.csv',
    'Config\ParachuteConfig.json',
    'Resources\Parachute.unity3d',
    'UIAtlases\ItemIconAtlas\modArmorParachute.png'
)
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $modSource $file))) { throw "Missing mod file: $file" }
}
$modBinary = Get-Item -LiteralPath (Join-Path $modSource 'Parachute.dll')
if ($modBinary.LastWriteTime -lt $buildStart.AddSeconds(-5)) {
    throw "Parachute.dll in Mod\Parachute was not refreshed by this build: $($modBinary.LastWriteTime)"
}

[xml]$modInfo = Get-Content -LiteralPath (Join-Path $modSource 'ModInfo.xml')
$version = $modInfo.xml.Version.value
if (-not $version) { throw 'ModInfo.xml has no Version value.' }

# File list: the mod folder without debug symbols, plus the README and the
# changelog, all under Parachute/.
$entries = [ordered]@{}
Get-ChildItem -LiteralPath $modSource -Recurse -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
    $relative = $_.FullName.Substring($modSource.Length + 1).Replace('\', '/')
    $entries["Parachute/$relative"] = $_.FullName
}
$entries['Parachute/README.md'] = Join-Path $repoRoot 'README.md'
$entries['Parachute/CHANGELOG.md'] = Join-Path $repoRoot 'CHANGELOG.md'

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$zipPath = Join-Path $distDir "Parachute-$version.zip"
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

# Written entry by entry with forward slashes; Compress-Archive in Windows
# PowerShell stores backslashes, which other unzip tools turn into odd names.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $entries.Keys) {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $entries[$name], $name, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $zip.Dispose() }

# Read the zip back and compare every entry with its source file.
$sha = [System.Security.Cryptography.SHA256]::Create()
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if ($zip.Entries.Count -ne $entries.Count) { throw "Zip has $($zip.Entries.Count) entries, expected $($entries.Count)." }
    foreach ($entry in $zip.Entries) {
        if (-not $entries.Contains($entry.FullName)) { throw "Unexpected zip entry: $($entry.FullName)" }
        $stream = $entry.Open()
        try { $packed = [BitConverter]::ToString($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
        $source = (Get-FileHash -LiteralPath $entries[$entry.FullName] -Algorithm SHA256).Hash
        if ($packed.Replace('-', '') -ne $source) { throw "Zip entry differs from its source: $($entry.FullName)" }
    }
}
finally { $zip.Dispose() }

$size = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 2)
Write-Host "Packaged Parachute $version ($($entries.Count) files, $size MB, verified): $zipPath"
Write-Host 'Install: unzip into the game''s Mods folder, giving Mods\Parachute\ModInfo.xml.'
