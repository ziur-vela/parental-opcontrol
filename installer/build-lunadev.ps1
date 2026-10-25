# Copyright (c) 2026 Ziur / OpControl IT Solutions
# SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
# Development-only hand-test package. Never ship this to a family.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$DotnetRoot = "C:\BuildTools\opcontrol-parental\dotnet",
    [string]$OutFile = ""
)
$ErrorActionPreference = "Stop"
$dotnet = Join-Path $DotnetRoot "dotnet.exe"
if (-not (Test-Path $dotnet)) { throw "SDK not found at $dotnet" }
if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path (Join-Path $RepoRoot "artifacts") "LunaDev.zip"
}
$work = Join-Path $env:TEMP ("lunadev-" + [Guid]::NewGuid().ToString("N"))
$luna = Join-Path $work "luna"
$helper = Join-Path $work "helper"
New-Item -ItemType Directory -Force $luna, $helper, (Split-Path $OutFile -Parent) | Out-Null
$env:DOTNET_ROOT = $DotnetRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Host "Publishing Debug Luna (dev endpoint enabled)..."
& $dotnet publish (Join-Path $RepoRoot "src\OpControl.Parental.Luna") -c Debug -r win-x64 -o $luna `
    -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing Debug Helper (lock preview enabled)..."
& $dotnet publish (Join-Path $RepoRoot "src\OpControl.Parental.Client.Windows.Helper") -c Debug -r win-x64 -o $helper
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$readme = @"
Luna AI development hand-test (Debug build only)

This build talks directly to the preview server http://100.109.25.49:8098
as the demo child Ana. It is NOT a Release build and must not be installed.

1. Luna app
   Run: OpControl.Parental.Luna.exe --dev
   - The sleeping moon appears in the top-left corner.
   - Hover over it for 0.4 seconds, or click it, to open the chat.
   - Use the moon/sun button to switch between Night sky and Moon gray.
   - Sleep-size hotkeys:
       Ctrl+Alt+Shift+Page Up    bigger
       Ctrl+Alt+Shift+Page Down  smaller
       Ctrl+Alt+Shift+Home       reset
   - The chosen size is saved on this PC.

2. My OpControl preview (kid view)
   Run: OpControl.Parental.Helper.exe --kid-preview --tab home --theme night --language en
   Tabs: home, schedule, rules, activity, messages
   Themes: night, gray
   Languages: en, es-MX
   The demo child is Ana; this reads no real data and changes no settings.
   Press Esc to close, or use the moon/sun button in the title bar to switch themes.

3. Lock screen preview
   Run: OpControl.Parental.Helper.exe --lock-preview --reason parent --language en --width 1600 --height 900
   Reasons: parent, bedtime, daily-limit, paused
   Languages: en, es-MX
   Try 3:2 with: --width 1620 --height 1080
   Press Esc to close.

No install, service, schedule or Windows setting is changed by this package.
"@
Set-Content -Path (Join-Path $work "README-LunaDev.txt") -Value $readme -Encoding UTF8

Copy-Item (Join-Path $luna "OpControl.Parental.Luna.exe") (Join-Path $work "OpControl.Parental.Luna.exe") -Force
Copy-Item (Join-Path $helper "OpControl.Parental.Helper.exe") (Join-Path $work "OpControl.Parental.Helper.exe") -Force
if (Test-Path $OutFile) { Remove-Item $OutFile -Force }
Compress-Archive -Path (Join-Path $work "OpControl.Parental.Luna.exe"), (Join-Path $work "OpControl.Parental.Helper.exe"), (Join-Path $work "README-LunaDev.txt") -DestinationPath $OutFile -CompressionLevel Optimal
Get-FileHash $OutFile -Algorithm SHA256 | Format-List Path, Hash
Get-Item $OutFile | Select-Object Name, Length
Remove-Item $work -Recurse -Force
