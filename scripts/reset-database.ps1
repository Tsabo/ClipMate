# ClipMate Database Reset Script
# This script removes old migrations and creates a fresh ClipMate 7.5 compatible migration

Write-Host "ClipMate Database Reset" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Remove existing migrations
Write-Host "Step 1: Removing old migrations..." -ForegroundColor Yellow
$migrationsPath = "..\Source\src\ClipMate.Data\Migrations"
if (Test-Path $migrationsPath) {
    Remove-Item -Path $migrationsPath -Recurse -Force
    Write-Host "  ✓ Old migrations deleted" -ForegroundColor Green
} else {
    Write-Host "  ℹ No migrations folder found" -ForegroundColor Gray
}

# Step 2: Delete the SQLite database
Write-Host ""
Write-Host "Step 2: Deleting SQLite database..." -ForegroundColor Yellow
$dbPath = "$env:LOCALAPPDATA\ClipMate\clipmate.db"
if (Test-Path $dbPath) {
    Remove-Item -Path $dbPath -Force
    Write-Host "  ✓ Database deleted: $dbPath" -ForegroundColor Green
} else {
    Write-Host "  ℹ Database not found (will be created fresh)" -ForegroundColor Gray
}

# Also delete any SQLite journal files
$dbJournal = "$dbPath-shm"
$dbWal = "$dbPath-wal"
if (Test-Path $dbJournal) { Remove-Item -Path $dbJournal -Force }
if (Test-Path $dbWal) { Remove-Item -Path $dbWal -Force }

# Step 3: Create new migration
Write-Host ""
Write-Host "Step 3: Creating new ClipMate 7.5 migration..." -ForegroundColor Yellow

# Create global.json to force .NET 9 SDK usage
$globalJsonPath = "..\Source\global.json"
$globalJsonContent = @{
    sdk = @{
        version = "9.0.307"
        rollForward = "latestMinor"
    }
} | ConvertTo-Json -Depth 10

Write-Host "  Creating global.json to use .NET 9 SDK..." -ForegroundColor Gray
Set-Content -Path $globalJsonPath -Value $globalJsonContent

Push-Location "..\Source\src\ClipMate.Data"
try {
    dotnet ef migrations add ClipMate75Schema --output-dir Migrations
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Migration 'ClipMate75Schema' created successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Migration creation failed" -ForegroundColor Red
        Write-Host "  Tip: Ensure .NET 9 SDK is installed (dotnet --list-sdks)" -ForegroundColor Yellow
        exit 1
    }
} finally {
    Pop-Location
    # Clean up global.json
    if (Test-Path $globalJsonPath) {
        Remove-Item -Path $globalJsonPath -Force
        Write-Host "  Cleaned up global.json" -ForegroundColor Gray
    }
}

# Step 4: Summary
Write-Host ""
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=======" -ForegroundColor Cyan
Write-Host "  • Old migrations: Deleted" -ForegroundColor Green
Write-Host "  • Database: Deleted (will be recreated on next run)" -ForegroundColor Green
Write-Host "  • New migration: ClipMate75Schema" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the migration in: src\ClipMate.Data\Migrations\" -ForegroundColor White
Write-Host "  2. Run the application - database will be created automatically" -ForegroundColor White
Write-Host "  3. Default ClipMate 7.5 collections will be seeded" -ForegroundColor White
Write-Host ""
Write-Host "✓ Database reset complete!" -ForegroundColor Green
