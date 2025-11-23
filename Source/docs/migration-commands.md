# Database Reset & Migration Commands

## Quick Reset (Recommended)

Run the PowerShell script:
```powershell
.\reset-database.ps1
```

This will:
1. Delete old migrations
2. Delete the SQLite database
3. Create a fresh `ClipMate75Schema` migration

---

## Manual Steps (Alternative)

### 1. Delete Old Migrations
```powershell
Remove-Item -Path "src\ClipMate.Data\Migrations" -Recurse -Force
```

### 2. Delete Database
```powershell
Remove-Item -Path "$env:LOCALAPPDATA\ClipMate\clipmate.db" -Force
```

### 3. Create New Migration
```bash
cd src/ClipMate.Data
dotnet ef migrations add ClipMate75Schema --output-dir Migrations
```

---

## After Migration

### Run the Application
The application will automatically:
1. Create the database using the migration
2. Seed default ClipMate 7.5 collections (13 collections)

### Verify Database
```bash
# Navigate to database location
cd $env:LOCALAPPDATA\ClipMate

# Open with SQLite browser
# or use command line:
sqlite3 clipmate.db
```

### Check Collections Seeded
```sql
SELECT Title, LmType, NewClipsGo FROM Collections ORDER BY SortKey;
```

Should show:
- Inbox (LmType=0, NewClipsGo=1) ← Active
- Safe (LmType=2)
- Overflow (LmType=0)
- Samples (LmType=2)
- Virtual (LmType=2)
  - Today (LmType=1)
  - This Week (LmType=1)
  - This Month (LmType=1)
  - Everything (LmType=1)
  - etc.

---

## Troubleshooting

### Migration Fails
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Try again
cd src/ClipMate.Data
dotnet ef migrations add ClipMate75Schema
```

### Database Locked
```powershell
# Stop the application first
# Then delete database
taskkill /F /IM ClipMate.App.exe
Remove-Item -Path "$env:LOCALAPPDATA\ClipMate\clipmate.db*" -Force
```

### Check EF Tools Installed
```bash
dotnet tool list --global
# If not installed:
dotnet tool install --global dotnet-ef
```

---

## What Gets Created

**Tables (14 total):**
- Clips (enhanced with 17 new fields)
- Collections (merged with Folders, 21 new fields)
- ClipData (NEW)
- BlobTxt, BlobJpg, BlobPng, BlobBlob (NEW - 4 tables)
- Shortcuts (NEW)
- Users (NEW)
- Templates, SearchQueries, ApplicationFilters, SoundEvents (existing)

**Indexes (19 new):**
- Performance optimized for ClipMate 7.5 operations

**Default Collections (13):**
- Exact ClipMate 7.5 GUIDs and structure

---

## Next Steps After Migration

1. ✅ Run application
2. ✅ Verify collections appear in tree view
3. ✅ Test clipboard capture
4. ✅ Verify clips go to Inbox
5. 🔨 Implement ClipData/BLOB storage (future)
6. 🔨 Implement PowerPaste shortcuts (future)
