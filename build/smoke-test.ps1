<#
.SYNOPSIS
    Starts ClipMate against an isolated data folder and checks that it gets through startup.

.DESCRIPTION
    Runs ClipMate.App.exe with --data-dir pointing at a fresh temporary folder, so the user's own settings, databases
    and Device identity are never touched. A first run there creates a default database without the setup wizard.
    Passes when the log reports "Initialization pipeline completed"; fails if the app exits, logs a fatal error, or
    does not finish within the timeout. On a timeout it prints the managed thread stacks (dotnet-stack) so a hang can
    be diagnosed, then stops the app.

.PARAMETER ExePath
    The ClipMate.App.exe to run. Defaults to the Debug build under Source\src\ClipMate.App\bin.

.PARAMETER DataDir
    The data folder to use. Defaults to a new folder under %TEMP%. Reusing a folder keeps its settings, so a second run
    can start with networking or pairing already set up.

.PARAMETER TimeoutSeconds
    How long to wait for startup to finish.

.PARAMETER KeepRunning
    Leave ClipMate running after a successful start (e.g. to pair two copies on one computer).

.EXAMPLE
    ./build/smoke-test.ps1

.EXAMPLE
    ./build/smoke-test.ps1 -ExePath C:\temp\artifacts\bin\ClipMate.App\debug\ClipMate.App.exe -KeepRunning
#>
[CmdletBinding()]
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\Source\src\ClipMate.App\bin\Debug\net10.0-windows\ClipMate.App.exe'),
    [string]$DataDir = (Join-Path ([IO.Path]::GetTempPath()) ("clipmate-smoke-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))),
    [int]$TimeoutSeconds = 90,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
$ExePath = (Resolve-Path $ExePath).Path
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
$logDir = Join-Path $DataDir 'Logs'

Write-Host "ClipMate: $ExePath"
Write-Host "Data dir: $DataDir"

$process = Start-Process -FilePath $ExePath -ArgumentList @('--data-dir', "`"$DataDir`"") -PassThru
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$result = $null

function Get-LogText {
    if (-not (Test-Path $logDir)) { return '' }
    # Logs roll to new files while the early and configured loggers share a name, so read them all
    (Get-ChildItem $logDir -Filter 'clipmate-*.log' | Sort-Object LastWriteTime | ForEach-Object {
        Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
    }) -join "`n"
}

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    $log = Get-LogText
    if ($log -match 'Initialization pipeline completed') { $result = 'started'; break }
    if ($log -match '\[FTL\]|Fatal error during application startup|Failed to build host') { $result = 'fatal'; break }
    if ($process.HasExited) { $result = 'exited'; break }
}

if (-not $result) { $result = 'timeout' }

if ($result -ne 'started') {
    Write-Host "SMOKE TEST FAILED: $result" -ForegroundColor Red
    Write-Host '--- last log lines ---'
    (Get-LogText) -split "`n" | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }

    if ($result -eq 'timeout' -and -not $process.HasExited) {
        # dotnet-stack is a local tool in .config/dotnet-tools.json ("dotnet tool restore" installs it)
        Write-Host '--- managed thread stacks (dotnet-stack) ---'
        Push-Location (Join-Path $PSScriptRoot '..')
        try { & dotnet tool run dotnet-stack report -p $process.Id }
        catch { Write-Host 'dotnet-stack is unavailable; run "dotnet tool restore".' }
        finally { Pop-Location }
    }

    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    exit 1
}

Write-Host "SMOKE TEST PASSED: startup completed (PID $($process.Id))" -ForegroundColor Green
if ($KeepRunning) {
    Write-Host "Left running. Stop it with: Stop-Process -Id $($process.Id)"
}
else {
    Stop-Process -Id $process.Id -Force
}
exit 0
