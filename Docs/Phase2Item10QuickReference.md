# Quick Reference: Data Retention Configuration

## Enable Retention with Defaults
Add to `appsettings.json`:
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": true
    }
  }
}
```
✅ Keeps 1 year of data, checks hourly, no archiving

---

## Basic Configuration
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": true,
      "MaxDays": 365,
      "CleanupIntervalMinutes": 60
    }
  }
}
```

---

## Archive Before Deleting
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": true,
      "MaxDays": 90,
      "CleanupIntervalMinutes": 1440,
      "ArchiveEnabled": true,
      "ArchiveDirectory": "C:\\StorageWatch_Archives",
      "ExportCsvEnabled": true
    }
  }
}
```

---

## Limit Database Size
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": true,
      "MaxDays": 3650,
      "MaxRows": 1000000,
      "CleanupIntervalMinutes": 360
    }
  }
}
```
Keeps either 10 years OR 1 million rows, whichever comes first

---

## Disable Retention
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": false
    }
  }
}
```
Database grows indefinitely (original behavior)

---

## Configuration Options Reference

| Option | Type | Min | Max | Default | Description |
|--------|------|-----|-----|---------|-------------|
| `Enabled` | bool | - | - | true | Enable automatic cleanup |
| `MaxDays` | int | 1 | 36500 | 365 | Days to keep log data |
| `MaxRows` | int | 0 | ∞ | 0 | Max rows (0=unlimited) |
| `CleanupIntervalMinutes` | int | 1 | 10080 | 60 | Minutes between cleanups |
| `ArchiveEnabled` | bool | - | - | false | Export CSV before delete |
| `ArchiveDirectory` | string | - | 500 | "" | Path for CSV archives |
| `ExportCsvEnabled` | bool | - | - | true | Export format (CSV) |

---

## Archive CSV Files

Located in `ArchiveDirectory`, named like:
```
DiskSpaceLog_Archive_20240115_143022.csv
DiskSpaceLog_Archive_20240116_143015.csv
```

Format:
```csv
Id,MachineName,DriveLetter,TotalSpaceGB,UsedSpaceGB,FreeSpaceGB,PercentFree,CollectionTimeUtc,CreatedAt
1,"MACHINE1","C:",1000.5,600.3,400.2,40.02,"2024-01-15T10:30:00Z","2024-01-15T10:30:01Z"
```

---

## Logging

Watch service logs for:
```
[Retention] Cleanup completed. Deleted 150 row(s).
[Retention WARNING] ArchiveDirectory is empty
[Retention ERROR] Cleanup operation failed
```

---

## Testing

Run unit tests:
```bash
dotnet test StorageWatch.Tests --filter "RetentionManagerTests"
```

Run integration tests:
```bash
dotnet test StorageWatch.Tests --filter "RetentionManagerIntegrationTests"
```

---

## Common Scenarios

### High-Frequency Monitoring (keep 7 days)
```json
"Retention": {
  "Enabled": true,
  "MaxDays": 7,
  "CleanupIntervalMinutes": 360
}
```

### Long-Term Archive (keep all, but backup)
```json
"Retention": {
  "Enabled": true,
  "MaxDays": 3650,
  "ArchiveEnabled": true,
  "ArchiveDirectory": "\\\\nas\\StorageWatch_Archive"
}
```

### Limited Disk (aggressive cleanup)
```json
"Retention": {
  "Enabled": true,
  "MaxDays": 30,
  "MaxRows": 50000,
  "CleanupIntervalMinutes": 60
}
```

---

## Default Configuration (if nothing specified)
```json
{
  "StorageWatch": {
    "Retention": {
      "Enabled": true,
      "MaxDays": 365,
      "MaxRows": 0,
      "CleanupIntervalMinutes": 60,
      "ArchiveEnabled": false,
      "ArchiveDirectory": "",
      "ExportCsvEnabled": true
    }
  }
}
```

---

## Performance

- Index on `CollectionTimeUtc`: ~100x faster cleanup
- Cleanup runs at configured interval only
- Non-blocking background operation
- CSV export only if archiving enabled

---

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| Data keeps growing | Enabled: false | Set to true in config |
| No cleanup running | Interval hasn't elapsed | Wait or reduce `CleanupIntervalMinutes` |
| Archive not created | Directory empty/invalid | Set valid `ArchiveDirectory` |
| Cleanup is slow | Large table, no index | Index creates automatically, wait for next run |

---

## Full Documentation

See `StorageWatch/Docs/Phase2Item10RetentionImplementation.md` for:
- Architecture details
- CSV format specification  
- Comprehensive logging examples
- Performance considerations
- Future enhancement ideas
