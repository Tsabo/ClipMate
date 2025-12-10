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
#>

[CmdletBinding()]
Param(
    [string]$Target = "Build",
    [string]$Version = "",
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Normal"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Determine script and repo root
$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "ClipMate Build Script" -ForegroundColor Cyan
Write-Host "Target: $Target" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
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
    $cakeArgs += "--version=$Version"
}

# Pass through any additional arguments
$cakeArgs += $args

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
