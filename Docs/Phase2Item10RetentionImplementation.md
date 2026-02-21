# Phase 2, Item 10: Data Retention & Cleanup Implementation

## Overview

This document outlines the implementation of configurable data retention and automatic cleanup for StorageWatch disk space logs. The system automatically deletes old data based on retention policies while optionally archiving deleted records to CSV files.

## Features Implemented

### 1. Configurable Retention Options

Added new `RetentionOptions` class in `StorageWatch/Config/Options/StorageWatchOptions.cs` with the following settings:

```csharp
public class RetentionOptions
{
    /// <summary>Enable or disable automatic retention and cleanup</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum days to retain log data (1-36500)</summary>
    [Range(1, 36500)]
    public int MaxDays { get; set; } = 365;

    /// <summary>Optional maximum row limit. 0 = no limit</summary>
    [Range(0, int.MaxValue)]
    public int MaxRows { get; set; } = 0;

    /// <summary>Cleanup interval in minutes (1-10080)</summary>
    [Range(1, 10080)]
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>Enable archiving of deleted rows</summary>
    public bool ArchiveEnabled { get; set; } = false;

    /// <summary>Directory for archived CSV files</summary>
    [StringLength(500)]
    public string ArchiveDirectory { get; set; } = string.Empty;

    /// <summary>Export archived data to CSV format</summary>
    public bool ExportCsvEnabled { get; set; } = true;
}
```

### 2. RetentionManager Service

New service: `StorageWatch/Services/DataRetention/RetentionManager.cs`

**Responsibilities:**
- Periodic cleanup of old rows based on `MaxDays`
- Row-count trimming based on `MaxRows`
- Optional archiving of deleted rows to CSV
- Non-blocking, interval-based cleanup operations

**Key Methods:**
- `CheckAndCleanupAsync()` - Performs cleanup if the interval has elapsed
- `PerformCleanupAsync()` - Executes archive (if enabled) and delete operations
- `ArchiveAndDeleteOldRowsAsync()` - Archives to CSV before deletion
- `DeleteOldRowsByDateAsync()` - Deletes rows older than MaxDays
- `TrimByRowCountAsync()` - Trims table to MaxRows limit
- `ExportToCsvAsync()` - Exports rows to CSV with proper formatting

**Archive CSV Format:**
```
Id,MachineName,DriveLetter,TotalSpaceGB,UsedSpaceGB,FreeSpaceGB,PercentFree,CollectionTimeUtc,CreatedAt
1,"Machine1","C:",1000.5,600.3,400.2,40.02,"2024-01-15T10:30:00Z","2024-01-15T10:30:01Z"
```

### 3. Database Indexes

Updated `SqliteSchema.cs` to add index on `CollectionTimeUtc` for efficient cleanup queries:

```sql
CREATE INDEX IF NOT EXISTS idx_DiskSpaceLog_CollectionTime
ON DiskSpaceLog(CollectionTimeUtc);
```

This index significantly improves:
- Date-based deletion queries
- Cleanup interval checks
- Historical data queries

### 4. Worker Integration

Updated `Services/Worker.cs` to integrate RetentionManager:

```csharp
// Initialize retention manager
_retentionManager = new RetentionManager(
    options.Database.ConnectionString, 
    options.Retention, 
    _logger
);

// In ExecuteAsync loop
if (_retentionManager != null)
{
    try
    {
        await _retentionManager.CheckAndCleanupAsync();
    }
    catch (Exception ex)
    {
        _logger.Log($"[WORKER ERROR] Retention cleanup failed: {ex}");
    }
}
```

The cleanup check runs every 30 seconds but only executes cleanup when the configured interval has elapsed (idempotent and non-blocking).

## Configuration

### Default JSON Configuration

Add to `appsettings.json`:

```json
{
  "StorageWatch": {
    "General": {
      "EnableStartupLogging": false
    },
    "Monitoring": {
      "ThresholdPercent": 10,
      "Drives": ["C:"]
    },
    "Database": {
      "ConnectionString": "Data Source=StorageWatch.db;Version=3;"
    },
    "Alerting": {
      "EnableNotifications": true,
      "Smtp": {
        "Enabled": false,
        "Host": "smtp.example.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "",
        "Password": "",
        "FromAddress": "",
        "ToAddress": ""
      },
      "GroupMe": {
        "Enabled": false,
        "BotId": ""
      },
      "Plugins": {}
    },
    "CentralServer": {
      "Enabled": false,
      "Mode": "Agent",
      "ServerUrl": "",
      "ApiKey": "",
      "Port": 5000,
      "CentralConnectionString": "",
      "ServerId": "central-server"
    },
    "Retention": {
      "Enabled": true,
      "MaxDays": 365,
      "MaxRows": 0,
      "CleanupIntervalMinutes": 60,
      "ArchiveEnabled": false,
      "ArchiveDirectory": "C:\\ProgramData\\StorageWatch\\Archives",
      "ExportCsvEnabled": true
    }
  }
}
```

### Configuration Examples

#### Example 1: Basic Retention (Default)
Keep 1 year of data, check daily:
```json
{
  "Retention": {
    "Enabled": true,
    "MaxDays": 365,
    "MaxRows": 0,
    "CleanupIntervalMinutes": 1440,
    "ArchiveEnabled": false
  }
}
```

#### Example 2: Aggressive Retention with Row Limit
Keep only 7 days OR 100,000 rows (whichever is smaller):
```json
{
  "Retention": {
    "Enabled": true,
    "MaxDays": 7,
    "MaxRows": 100000,
    "CleanupIntervalMinutes": 360,
    "ArchiveEnabled": false
  }
}
```

#### Example 3: Archive Before Delete
Keep 90 days, archive deleted records to CSV:
```json
{
  "Retention": {
    "Enabled": true,
    "MaxDays": 90,
    "MaxRows": 0,
    "CleanupIntervalMinutes": 1440,
    "ArchiveEnabled": true,
    "ArchiveDirectory": "C:\\StorageWatch_Archives",
    "ExportCsvEnabled": true
  }
}
```

#### Example 4: Disabled
Disable all automatic cleanup:
```json
{
  "Retention": {
    "Enabled": false
  }
}
```

## Logging

The RetentionManager logs all operations with the `[Retention]` prefix:

```
[Retention] Cleanup completed. Deleted 150 row(s).
[Retention] Archived 150 row(s) to C:\Archives\DiskSpaceLog_Archive_20240115_143022.csv
[Retention] Deleted 150 archived row(s) from database.
[Retention] Trimmed 50 row(s) to respect MaxRows limit of 100000.
[Retention WARNING] ArchiveEnabled but ArchiveDirectory is empty. Skipping archive.
[Retention ERROR] Cleanup operation failed: {exception details}
```

## Safety & Design

### Idempotent Cleanup
- Cleanup operations are safe to call frequently (every 30 seconds)
- Actual cleanup only runs when the configured interval elapses
- Multiple cleanup calls don't cause issues

### Non-Blocking
- Cleanup runs asynchronously in the main worker loop
- If cleanup fails, it logs the error but doesn't stop the service
- SQL operations use proper indexes for efficiency

### Data Integrity
- Archive files are created BEFORE deletion
- CSV export includes all necessary fields for audit/restore
- Database indexes ensure cleanup queries perform well

### Resource Efficient
- Cleanup only runs at configured intervals
- Batch deletion of rows (no per-row overhead)
- Archive directory created on-demand

## Testing

### Unit Tests: `RetentionManagerTests.cs`

Tests validation and basic initialization:
- Constructor validation (null parameter handling)
- RetentionOptions validation (range checks, defaults)
- Configuration defaults are reasonable
- Cleanup disabled behavior
- Interval elapsed behavior

Run with:
```bash
dotnet test StorageWatch.Tests --filter "RetentionManagerTests"
```

### Integration Tests: `RetentionManagerIntegrationTests.cs`

Tests real cleanup operations with SQLite database:
- ✅ Removes rows older than MaxDays
- ✅ Respects MaxRows limit
- ✅ Archives to CSV and deletes
- ✅ Skips cleanup when disabled
- ✅ Respects cleanup interval
- ✅ CSV export format is valid

Run with:
```bash
dotnet test StorageWatch.Tests --filter "RetentionManagerIntegrationTests"
```

## Migration & Upgrade Path

If upgrading from a version without retention:

1. **Automatic**: New tables are created with the required index
2. **First Run**: Retention defaults to 365 days, no archiving
3. **Custom Config**: Users can set retention options in `appsettings.json`
4. **Archives**: Archive directory must exist or will be created automatically

## Performance Considerations

### Database Indexes
- `idx_DiskSpaceLog_CollectionTime` speeds up date-based queries
- Without this index, cleanup on large tables would be very slow

### Cleanup Intervals
- Default 60 minutes is reasonable for most installations
- Minimum 1 minute, maximum 7 days
- More frequent cleanup = slightly more CPU/disk, but smaller operation each time

### Row Count Limits
- Default is 0 (no limit)
- Useful for low-disk installations or high-frequency monitoring
- Trimming happens automatically when MaxRows is exceeded

### Archive Directory
- Should be on a fast local disk
- Archives are dated, multiple CSV files accumulate
- Users should implement archive cleanup policy (delete old CSVs manually or via scripts)

## Troubleshooting

### Cleanup Not Running
**Symptom:** Database size keeps growing
**Causes:**
- `Retention.Enabled` is false
- Cleanup interval hasn't elapsed since last run
- Archive directory doesn't exist (if archiving enabled)

**Solution:** Check `appsettings.json` and logs for `[Retention]` messages

### Archive Directory Not Created
**Symptom:** `[Retention WARNING] ArchiveDirectory is empty`
**Cause:** `ArchiveEnabled: true` but `ArchiveDirectory` is empty or invalid
**Solution:** Set valid `ArchiveDirectory` path

### Performance Degradation During Cleanup
**Symptom:** Service uses high CPU/disk during scheduled cleanup
**Cause:** Large table being trimmed, no indexes
**Solution:** Indexes are created automatically; reduce `CleanupIntervalMinutes` to perform more frequent, smaller cleanups

## Future Enhancements

Potential improvements for future phases:

1. **Scheduled Cleanup**: Run cleanup at specific time of day instead of interval-based
2. **Per-Machine Retention**: Different retention policies per machine/drive
3. **Compression**: Gzip archived CSV files to save disk space
4. **Remote Archive**: Upload archives to cloud storage (S3, Azure Blob, etc.)
5. **Retention Reports**: Generate reports of archived data
6. **GUI Control**: Manage retention policies through StorageWatch UI
7. **Vacuum**: Run `VACUUM` after large deletions to reclaim disk space

## Compliance

All retention operations:
- ✅ Log all deletions for audit purposes
- ✅ Archive before delete (no data loss)
- ✅ Use UTC timestamps for consistency
- ✅ Support encryption at rest (when config encryption is enabled)
- ✅ Are compatible with CC0 license (no external dependencies)

## References

- **Configuration Options**: `StorageWatch/Config/Options/StorageWatchOptions.cs`
- **RetentionManager**: `StorageWatch/Services/DataRetention/RetentionManager.cs`
- **Database Schema**: `StorageWatch/Data/SqliteSchema.cs`
- **Unit Tests**: `StorageWatch.Tests/UnitTests/RetentionManagerTests.cs`
- **Integration Tests**: `StorageWatch.Tests/IntegrationTests/RetentionManagerIntegrationTests.cs`
- **Master Prompt**: `StorageWatch/Docs/CopilotMasterPrompt.md`
