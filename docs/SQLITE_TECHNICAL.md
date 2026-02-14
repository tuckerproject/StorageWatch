# SQLite Implementation Details

## Architecture Changes

### Data Access Pattern

**Before (SQL Server)**
```csharp
using var connection = new SqlConnection(_config.Database.ConnectionString);
await connection.OpenAsync();
// ... execute commands
```

**After (SQLite)**
```csharp
using var connection = new SqliteConnection(_config.Database.ConnectionString);
await connection.OpenAsync();
// ... execute commands
```

## Implementation Components

### 1. SqliteSchema (New)
**File**: `StorageWatch/Data/SqliteSchema.cs`

Handles automatic database initialization with the following responsibilities:
- Creates SQLite database file if it doesn't exist
- Creates `DiskSpaceLog` table with proper schema
- Creates performance index for efficient queries
- Logs all operations for troubleshooting
- Throws exceptions on critical failures

**Key Methods**:
- `InitializeDatabaseAsync()`: Performs full database setup

### 2. SqlReporter (Updated)
**File**: `StorageWatch/Data/SqlReporter.cs`

Updated to use SQLite instead of SQL Server with these changes:
- Replaced `SqlConnection` → `SqliteConnection`
- Replaced `SqlCommand` → `SqliteCommand`
- Changed import from `Microsoft.Data.SqlClient` to `Microsoft.Data.Sqlite`
- SQL statements remain unchanged (both systems support the same INSERT syntax)

**Key Methods**:
- `WriteDailyReportAsync()`: Writes disk space metrics to SQLite
- `GetDiskStatus()`: Queries current drive metrics

### 3. Worker Service (Updated)
**File**: `StorageWatch/Services/Worker.cs`

Enhanced with database initialization during startup:
- Calls `SqliteSchema.InitializeDatabaseAsync()` in constructor
- Ensures database exists before any operations
- Provides startup logging for verification
- Handles initialization failures gracefully

### 4. Configuration (Updated)
**File**: `StorageWatch/Config/StorageWatchConfig.cs`

Documentation updated to reflect SQLite usage:
- `DatabaseConfig.ConnectionString` now represents SQLite connection format
- Example: `Data Source=StorageWatch.db;Version=3;`

**File**: `StorageWatch/StorageWatchConfig.xml`

Connection string updated:
```xml
<Database>
    <ConnectionString>Data Source=StorageWatch.db;Version=3;</ConnectionString>
</Database>
```

## Connection String Formats

### SQLite Connection String Options

**Basic (Default)**
```
Data Source=StorageWatch.db;Version=3;
```

**With Absolute Path**
```
Data Source=C:\ProgramData\StorageWatch\StorageWatch.db;Version=3;
```

**With Additional Options**
```
Data Source=StorageWatch.db;Version=3;Cache=Shared;
```

### Recommended Configuration
For Windows Service installation, use the program data directory:
```
Data Source=C:\ProgramData\StorageWatch\StorageWatch.db;Version=3;
```

## Database Schema

### DiskSpaceLog Table
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
```

### Performance Index
```sql
CREATE INDEX idx_DiskSpaceLog_Machine_Drive_Time
ON DiskSpaceLog(MachineName, DriveLetter, CollectionTimeUtc);
```

**Purpose**: Accelerates queries filtering by machine, drive, and time range

**Query Pattern**:
```sql
SELECT * FROM DiskSpaceLog 
WHERE MachineName = @machine 
  AND DriveLetter = @drive 
  AND CollectionTimeUtc BETWEEN @start AND @end
```

## Data Consistency

### Timestamp Handling
- All timestamps stored in UTC: `DateTime.UtcNow`
- Ensures consistency across time zones
- SQLite stores DateTime as TEXT in ISO 8601 format

### Parameter Binding
All queries use parameterized statements to prevent SQL injection:
```csharp
command.Parameters.AddWithValue("@machine", machineName);
command.Parameters.AddWithValue("@drive", driveLetter);
// ... etc
```

### Transaction Support
Current implementation uses auto-commit mode (single statements). For future bulk operations, SQLite supports explicit transactions:
```csharp
using var transaction = connection.BeginTransaction();
// ... execute multiple commands
await transaction.CommitAsync();
```

## Performance Characteristics

### SQLite vs SQL Server

| Aspect | SQLite | SQL Server |
|--------|--------|-----------|
| **Deployment** | Single file | Server installation |
| **Startup** | Instant | Requires service |
| **Concurrent Writers** | Limited (sequential) | Full ACID |
| **Data Size** | Excellent for GBs | Excellent for TBs+ |
| **Embedded Use** | Perfect | Not applicable |
| **Resource Usage** | Minimal | Significant |

**For StorageWatch**: SQLite is ideal since:
- Data is written sequentially (once per day collection)
- Single machine local storage
- No concurrent write contention
- Small-to-medium data volumes

## Error Handling

### Initialization Errors
```csharp
try
{
    schemaInitializer.InitializeDatabaseAsync().Wait();
}
catch (Exception ex)
{
    _logger.Log($"[STARTUP ERROR] Failed to initialize SQLite database: {ex}");
    throw; // Service startup fails
}
```

### Operation Errors
```csharp
try
{
    await command.ExecuteNonQueryAsync();
}
catch (Exception ex)
{
    _logger.Log($"[SQL ERROR] Failed to insert row: {ex}");
    // Continue with next drive
}
```

## Future Enhancements

### Potential Improvements
1. **Query Interface**: Add repository pattern for data access
2. **Retention Policy**: Automatic cleanup of old data
3. **Bulk Insert**: Batch multiple records for efficiency
4. **Replication**: Optional SQLite replication to central server
5. **Encryption**: Database file encryption support

### Migration to Server Mode
When Phase 1, Item 4 introduces Agent/Server architecture:
- Agents continue using local SQLite
- Server receives data from agents
- Server maintains centralized database (could be SQL Server or SQLite)

## Troubleshooting

### Common Issues

**Issue**: "database is locked"
- **Cause**: Multiple processes accessing database simultaneously
- **Solution**: SQLite locks entire file during writes (normal behavior)

**Issue**: "cannot open shared object file"
- **Cause**: Missing SQLite runtime dependencies
- **Solution**: .NET 10 includes SQLite support built-in

**Issue**: Database file size grows rapidly
- **Cause**: No retention policy yet (Phase 1, Item 6+)
- **Solution**: Periodically archive or delete old records

### Debugging

View database contents:
```powershell
# Using DB Browser for SQLite
& "C:\Program Files\DB Browser for SQLite\DB Browser for SQLite.exe" C:\ProgramData\StorageWatch\StorageWatch.db
```

Query via PowerShell:
```powershell
$dbPath = "C:\ProgramData\StorageWatch\StorageWatch.db"
$conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$dbPath;Version=3;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM DiskSpaceLog"
$cmd.ExecuteScalar()
$conn.Close()
```

## References

- [Microsoft.Data.Sqlite Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite/)
- [SQLite Official Site](https://www.sqlite.org/)
- [SQLite Query Language](https://www.sqlite.org/lang.html)
