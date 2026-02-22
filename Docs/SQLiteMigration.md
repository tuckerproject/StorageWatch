# SQLite Migration Guide

## Overview

StorageWatch has transitioned from SQL Server to SQLite for local data storage. This change simplifies deployment, eliminates external database dependencies, and makes the application more portable while maintaining full functionality for disk monitoring and alerting.

## Key Changes

### Database Technology
- **Previous**: SQL Server (requires separate server installation and configuration)
- **New**: SQLite (embedded, file-based database)

### Database Location
- **Default**: `StorageWatch.db` in the application directory
- **Configurable**: You can specify a custom path in `StorageWatchConfig.xml`

## Migration Steps for Existing SQL Server Users

### 1. Export Your Historical Data (Optional)

If you have historical disk space data in your existing SQL Server database that you want to preserve:

```sql
-- SQL Server query to export your data
SELECT MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc
FROM DiskSpaceLog
ORDER BY CollectionTimeUtc DESC
```

Export this as CSV or JSON for archival purposes.

### 2. Update Configuration File

Update your `StorageWatchConfig.xml`:

**Before (SQL Server):**
```xml
<Database>
    <ConnectionString>
        Server=localhost;Database=Disk_Space_DB;Trusted_Connection=True;TrustServerCertificate=True;
    </ConnectionString>
</Database>
```

**After (SQLite):**
```xml
<Database>
    <ConnectionString>
        Data Source=StorageWatch.db;Version=3;
    </ConnectionString>
</Database>
```

Or with a full path:
```xml
<Database>
    <ConnectionString>
        Data Source=C:\ProgramData\StorageWatch\StorageWatch.db;Version=3;
    </ConnectionString>
</Database>
```

### 3. Restart the Service

```powershell
# Stop the service
Stop-Service -Name StorageWatch

# Start the service
Start-Service -Name StorageWatch
```

The application will automatically create the SQLite database and schema on first run.

### 4. Verify Migration

Check that:
- The `StorageWatch.db` file exists in your specified location
- The service is running without errors
- New disk space data is being recorded (check logs)

## Database Schema

The SQLite schema is automatically created with the following structure:

```sql
CREATE TABLE DiskSpaceLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineName TEXT NOT NULL,
    DriveLetter TEXT NOT NULL,
    TotalSpaceGB REAL NOT NULL,
    UsedSpaceGB REAL NOT NULL,
    FreeSpaceGB REAL NOT NULL,
    PercentFree REAL NOT NULL,
    CollectionTimeUtc DATETIME NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_DiskSpaceLog_Machine_Drive_Time
ON DiskSpaceLog(MachineName, DriveLetter, CollectionTimeUtc);
```

## Benefits of SQLite

1. **No External Dependencies**: No SQL Server installation required
2. **Portable**: Single file database
3. **Lower Resource Usage**: Minimal memory and CPU footprint
4. **Offline Capable**: Works without network connectivity
5. **Easy Backup**: Simply copy the `.db` file
6. **Public Domain**: No licensing concerns

## Troubleshooting

### Database File Permissions
Ensure the Windows Service account has read/write access to the database file location.

### Connection String Issues
If using a custom path, ensure the directory exists and the path is properly formatted:
```
Data Source=C:\Path\To\StorageWatch.db;Version=3;
```

### Checking Database Contents

You can view the database contents using free tools like:
- SQLite Browser (sqlitebrowser.org)
- DB Browser for SQLite
- Visual Studio Code with SQLite extension

## Reverting to SQL Server (Not Recommended)

If you need to temporarily keep SQL Server support, you can:
1. Maintain the old SQL Server connection string configuration
2. Manually update the code to support both database types

However, the recommended approach is to fully migrate to SQLite as the default storage mechanism.

## Questions or Issues?

See the main README.md or submit an issue on the project repository.
