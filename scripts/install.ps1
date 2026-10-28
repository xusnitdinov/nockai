#Requires -Version 5.1
<#
.SYNOPSIS
  Build NockAI (Release) and install to %LOCALAPPDATA%\NockAI
.NOTES
  Needs .NET 9 SDK: winget install Microsoft.DotNet.SDK.9
#>
param(
    [switch]$NoStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Proj = Join-Path $Root "src\NockAI\NockAI.csproj"
$Out = Join-Path $env:LOCALAPPDATA "NockAI"
$Exe = Join-Path $Out "NockAI.exe"

if (-not (Test-Path $Proj)) {
    throw "Project not found: $Proj"
}

Write-Host "NockAI install" -ForegroundColor Cyan
Write-Host "  project: $Proj"
Write-Host "  target:  $Out"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install with: winget install Microsoft.DotNet.SDK.9"
}

Write-Host "`nBuilding Release..." -ForegroundColor Yellow
dotnet publish $Proj -c Release -r win-x64 --self-contained false -o $Out
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$secretsUser = Join-Path $env:LOCALAPPDATA "NockAI\Secrets.local.json"
$example = Join-Path $Root "src\NockAI\Secrets.local.json.example"
if (-not (Test-Path $secretsUser) -and (Test-Path $example)) {
    Copy-Item $example (Join-Path $Out "Secrets.local.json.example") -Force
    Write-Host "No API key yet - first launch will ask for a Groq key." -ForegroundColor DarkYellow
}

$w = New-Object -ComObject WScript.Shell

# Start Menu (so Windows Search finds it under Apps)
$programs = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuLnk = Join-Path $programs "NockAI.lnk"
$ico = Join-Path $Out "Assets\app.ico"
if (-not (Test-Path $ico)) { $ico = Join-Path $Out "app.ico" }

$menu = $w.CreateShortcut($startMenuLnk)
$menu.TargetPath = $Exe
$menu.WorkingDirectory = $Out
$menu.Description = "NockAI - Ctrl+Space calculator"
if (Test-Path $ico) { $menu.IconLocation = "$ico,0" }
$menu.Save()
Write-Host "Start Menu shortcut: $startMenuLnk" -ForegroundColor Green

if (-not $NoStartup) {
    $startup = [Environment]::GetFolderPath("Startup")
    $lnkPath = Join-Path $startup "NockAI.lnk"
    $lnk = $w.CreateShortcut($lnkPath)
    $lnk.TargetPath = $Exe
    $lnk.WorkingDirectory = $Out
    $lnk.Description = "NockAI - Ctrl+Space calculator"
    if (Test-Path $ico) { $lnk.IconLocation = "$ico,0" }
    $lnk.Save()
    Write-Host "Startup shortcut: $lnkPath" -ForegroundColor Green
}

Write-Host "`nInstalled: $Exe" -ForegroundColor Green
Write-Host "Hotkey: Ctrl+Space   |   Esc to hide   |   Ctrl+C copies answer"

if (-not $NoLaunch) {
    Start-Process $Exe
    Write-Host "Launched."
}
