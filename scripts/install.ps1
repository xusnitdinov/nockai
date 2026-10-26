#Requires -Version 5.1
<#
.SYNOPSIS
  Build QuickCalcAI (Release) and install to %LOCALAPPDATA%\QuickCalcAI
.NOTES
  Needs .NET 9 SDK: winget install Microsoft.DotNet.SDK.9
#>
param(
    [switch]$NoStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Proj = Join-Path $Root "src\QuickCalcAI\QuickCalcAI.csproj"
$Out = Join-Path $env:LOCALAPPDATA "QuickCalcAI"
$Exe = Join-Path $Out "QuickCalcAI.exe"

if (-not (Test-Path $Proj)) {
    throw "Project not found: $Proj"
}

Write-Host "QuickCalcAI install" -ForegroundColor Cyan
Write-Host "  project: $Proj"
Write-Host "  target:  $Out"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install with: winget install Microsoft.DotNet.SDK.9"
}

Write-Host "`nBuilding Release..." -ForegroundColor Yellow
dotnet publish $Proj -c Release -r win-x64 --self-contained false -o $Out
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$secretsUser = Join-Path $env:LOCALAPPDATA "QuickCalcAI\Secrets.local.json"
$example = Join-Path $Root "src\QuickCalcAI\Secrets.local.json.example"
if (-not (Test-Path $secretsUser) -and (Test-Path $example)) {
    Copy-Item $example (Join-Path $Out "Secrets.local.json.example") -Force
    Write-Host "No API key yet - first launch will ask for a Groq key." -ForegroundColor DarkYellow
}

if (-not $NoStartup) {
    $startup = [Environment]::GetFolderPath("Startup")
    $lnkPath = Join-Path $startup "QuickCalcAI.lnk"
    $w = New-Object -ComObject WScript.Shell
    $lnk = $w.CreateShortcut($lnkPath)
    $lnk.TargetPath = $Exe
    $lnk.WorkingDirectory = $Out
    $lnk.Description = "QuickCalcAI - Ctrl+Space calculator"
    $lnk.Save()
    Write-Host "Startup shortcut: $lnkPath" -ForegroundColor Green
}

Write-Host "`nInstalled: $Exe" -ForegroundColor Green
Write-Host "Hotkey: Ctrl+Space   |   Esc to hide   |   Ctrl+C copies answer"

if (-not $NoLaunch) {
    Start-Process $Exe
    Write-Host "Launched."
}
