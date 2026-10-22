#!/usr/bin/env pwsh
<#
.SYNOPSIS
ClipMate build script bootstrapper.

.DESCRIPTION
This script bootstraps the Cake build system and executes the build.cake script.

.PARAMETER Target
The build target to execute (e.g., Build, Test, CI, Release).

.PARAMETER Version
Override the auto-detected version.

.PARAMETER Configuration
The build configuration (Debug or Release). Default is Release.

.PARAMETER Verbosity
The logging verbosity level. Default is Normal.

.EXAMPLE
.\build.ps1 -Target Build
.\build.ps1 -Target Release -Version "1.0.0"
.\build.ps1 -Verbosity Diagnostic
.\build.ps1 --target Build --verbosity Diagnostic
.\build.ps1 -Target Smoke-Test -Configuration Debug --keep-running

Arguments the script does not recognise are passed to the Cake target.
#>

[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$Target = "Build",
    [string]$Version = "",
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Normal",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Determine script and repo root
$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = Split-Path -Parent $PSScriptRoot

# Process remaining arguments to handle double-dash syntax
# This allows: .\build.ps1 --target Build --verbosity Diagnostic
$PassThroughArguments = @()
if ($RemainingArguments) {
    for ($i = 0; $i -lt $RemainingArguments.Length; $i++) {
        $arg = $RemainingArguments[$i]
        
        switch -Regex ($arg) {
            '^--target$|^-t$' {
                if ($i + 1 -lt $RemainingArguments.Length) {
                    $Target = $RemainingArguments[++$i]
                }
            }
            '^--configuration$|^-c$' {
                if ($i + 1 -lt $RemainingArguments.Length) {
                    $Configuration = $RemainingArguments[++$i]
                }
            }
            '^--version$|^-v$' {
                if ($i + 1 -lt $RemainingArguments.Length) {
                    $Version = $RemainingArguments[++$i]
                }
            }
            '^--verbosity$' {
                if ($i + 1 -lt $RemainingArguments.Length) {
                    $Verbosity = $RemainingArguments[++$i]
                }
            }
            # Anything else is a target's own argument (e.g. --keep-running, --test-filter=...): pass it to Cake
            default {
                $PassThroughArguments += $arg
            }
        }
    }
}

Write-Host "ClipMate Build Script" -ForegroundColor Cyan
Write-Host "Target: $Target" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
if ($Verbosity -ne "Normal") {
    Write-Host "Verbosity: $Verbosity" -ForegroundColor Gray
}
Write-Host ""

# Ensure Cake tool is installed
Write-Host "Restoring .NET tools..." -ForegroundColor Yellow
Push-Location $RepoRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to restore .NET tools" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
Write-Host "Tools restored" -ForegroundColor Green
Write-Host ""

# Build the argument list for Cake
$cakeArgs = @(
    "cake"
    "$PSScriptRoot/build.cake"
    "--target=$Target"
    "--configuration=$Configuration"
    "--verbosity=$Verbosity"
)

if ($Version) {
    $cakeArgs += "--release-version=$Version"
}

$cakeArgs += $PassThroughArguments

# Execute Cake
Write-Host "Executing Cake build..." -ForegroundColor Yellow
Write-Host "Command: dotnet $($cakeArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

Push-Location $RepoRoot
try {
    & dotnet @cakeArgs
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "Build completed successfully!" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "Build failed with exit code: $exitCode" -ForegroundColor Red
    }
    
    exit $exitCode
}
finally {
    Pop-Location
}
