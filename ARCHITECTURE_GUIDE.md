# StorageWatch Server Architecture Guide

## Quick Reference

### Database Location
**Path:** `C:\ProgramData\StorageWatch\Server\StorageWatchServer.db`

### New API Endpoint
**POST /api/agent/report**
```json
{
  "machineName": "COMPUTER-01",
  "rows": [
    {
      "driveLetter": "C:",
      "totalSpaceGb": 500.0,
      "usedSpaceGb": 250.0,
      "freeSpaceGb": 250.0,
      "percentFree": 50.0,
      "timestamp": "2024-01-15T10:30:00Z"
    }
  ]
}
```

### Response
- **200 OK:** Batch accepted and processed
- **400 Bad Request:** Validation error (malformed input)
- **500 Internal Server Error:** Server-side error

---

## Architecture Overview

### Key Principle: Passive Raw-Row Ingestion
The Server is now a **passive receiver** of raw data. It:
1. ✅ Accepts batches of raw rows exactly as the Agent sends them
2. ✅ Stores rows as-is without transformation
3. ✅ Provides views that group and display raw data
4. ❌ Does NOT aggregate or normalize data at ingestion time
5. ❌ Does NOT modify timestamps or values
6. ❌ Does NOT deduplicate or validate for consistency

---

## Data Model

### RawDriveRow (Database Entity)
```csharp
public class RawDriveRow
{
    public long Id { get; set; }                    // Auto-incremented primary key
    public string MachineName { get; set; }         // Machine name from Agent
    public string DriveLetter { get; set; }         // e.g., "C:"
    public double TotalSpaceGb { get; set; }        // Total capacity
    public double UsedSpaceGb { get; set; }         // Used space
    public double FreeSpaceGb { get; set; }         // Free space
    public double PercentFree { get; set; }         // Free percentage (0-100)
    public DateTime Timestamp { get; set; }         // Collection timestamp (UTC)
}
```

### Database Schema
```sql
CREATE TABLE RawDriveRows (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineName TEXT NOT NULL,
    DriveLetter TEXT NOT NULL,
    TotalSpaceGb REAL NOT NULL,
    UsedSpaceGb REAL NOT NULL,
    FreeSpaceGb REAL NOT NULL,
    PercentFree REAL NOT NULL,
    Timestamp DATETIME NOT NULL
);

CREATE INDEX idx_RawDriveRows_Machine_Time
    ON RawDriveRows(MachineName, Timestamp DESC);
```

---

## Service Layer

### RawRowIngestionService
**Location:** `StorageWatchServer/Server/Reporting/RawRowIngestionService.cs`

**Responsibility:** Insert raw rows into database as-is

**Key Method:**
```csharp
public async Task IngestRawRowsAsync(string machineName, List<RawDriveRow> rows)
```

**Behavior:**
- Validates input (machineName non-empty, rows non-empty)
- Opens SQLite connection
- Uses transaction for batch insertion
- Inserts each row as received
- No transformation or validation of row values

**Example Usage:**
```csharp
var ingestionService = /* injected */;
var rows = new List<RawDriveRow>
{
    new RawDriveRow
    {
        DriveLetter = "C:",
        TotalSpaceGb = 500,
        UsedSpaceGb = 250,
        FreeSpaceGb = 250,
        PercentFree = 50,
        Timestamp = DateTime.UtcNow
    }
};

await ingestionService.IngestRawRowsAsync("MY-COMPUTER", rows);
```

---

## Controller Layer

### RawRowsController
**Location:** `StorageWatchServer/Controllers/RawRowsController.cs`

**Route:** `POST /api/agent/report`

**Responsibilities:**
1. Parse and validate request
2. Convert request DTOs to domain model
3. Call ingestion service
4. Return appropriate HTTP status

**Validation Rules:**
- `machineName`: Required, non-empty string
- `rows`: Required, must be non-null array with at least one element
- Each row's `driveLetter`: Required, non-empty string

**Example Request:**
```bash
curl -X POST http://localhost:5001/api/agent/report \
  -H "Content-Type: application/json" \
  -d '{
    "machineName": "COMPUTER-01",
    "rows": [
      {
        "driveLetter": "C:",
        "totalSpaceGb": 500,
        "usedSpaceGb": 250,
        "freeSpaceGb": 250,
        "percentFree": 50,
        "timestamp": "2024-01-15T10:30:00Z"
      }
    ]
  }'
```

---

## Dashboard Integration

### Reports Page (Razor Pages)
**Location:** `StorageWatchServer/Dashboard/Reports.cshtml.cs`

**Model:** `ReportsModel`

**Key Properties:**
- `RecentReportsByMachine`: List<MachineReportGroup>
  - `MachineName`: The machine name
  - `LatestTimestamp`: Most recent report timestamp
  - `Rows`: List of raw drive rows from that report

**Data Loading Logic:**
1. Query RawDriveRows grouped by MachineName
2. Get the MAX(Timestamp) for each machine
3. Limit to 50 most recent machines
4. For each machine, fetch all rows with that MachineName and LatestTimestamp
5. Display in MachineReportGroup format

**Example Usage in View:**
```html
@foreach (var group in Model.RecentReportsByMachine)
{
    <h3>@group.MachineName</h3>
    <p>Latest: @group.LatestTimestamp.ToString("o")</p>
    <ul>
        @foreach (var row in group.Rows)
        {
            <li>
                @row.DriveLetter: @row.FreeSpaceGb GB free / 
                @row.TotalSpaceGb GB total (@row.PercentFree% free)
            </li>
        }
    </ul>
}
```

---

## Configuration

### ServerOptions
**Location:** `StorageWatchServer/Server/Services/ServerOptions.cs`

**Properties:**
```csharp
public string ListenUrl { get; set; }           // Default: http://localhost:5001
public string DatabasePath { get; set; }        // Default: C:\ProgramData\StorageWatch\Server\StorageWatchServer.db
public int OnlineTimeoutMinutes { get; set; }   // Default: 10
```

**Configuration Source:** `ServerConfig.json` in ProgramData/StorageWatch/Server/

**Default Configuration:**
```json
{
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "C:\\ProgramData\\StorageWatch\\Server\\StorageWatchServer.db",
    "OnlineTimeoutMinutes": 10
  }
}
```

---

## Database Initialization

### ServerSchema
**Location:** `StorageWatchServer/Server/Data/ServerSchema.cs`

**Method:** `public async Task InitializeDatabaseAsync()`

**What It Does:**
1. Ensures directory exists for database file
2. Creates connection to SQLite database
3. Enables foreign keys
4. Creates RawDriveRows table with index
5. Creates Machines table (for tracking unique machines)
6. Creates Settings table (for configuration)

**When It Runs:**
- Automatically called on application startup in `Program.cs`
- Creates database file silently if it doesn't exist
- Uses "CREATE TABLE IF NOT EXISTS" to be idempotent

---

## Dependency Injection

### Service Registration (Program.cs)
```csharp
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServerOptions>>().Value);
builder.Services.AddSingleton<ServerSchema>();
builder.Services.AddSingleton<ServerRepository>();
builder.Services.AddSingleton<RawRowIngestionService>();
```

### Injection in Controllers/Services
```csharp
public class RawRowsController : ControllerBase
{
    private readonly RawRowIngestionService _ingestionService;
    private readonly ILogger<RawRowsController> _logger;

    public RawRowsController(
        RawRowIngestionService ingestionService,
        ILogger<RawRowsController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }
}
```

---

## Common Tasks

### Add a New View/Query for Raw Rows
```csharp
// In a service or Razor page
await using var connection = new SqliteConnection(GetConnectionString());
await connection.OpenAsync();

var sql = @"
    SELECT * FROM RawDriveRows
    WHERE MachineName = @machineName
    ORDER BY Timestamp DESC
    LIMIT 100;
";

await using (var command = new SqliteCommand(sql, connection))
{
    command.Parameters.AddWithValue("@machineName", "MY-COMPUTER");
    var rows = new List<RawDriveRow>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        rows.Add(new RawDriveRow
        {
            // ... map columns to properties
        });
    }
}
```

### Query for Machines with Low Free Space
```csharp
// Get latest report per machine with drives < 20% free
var sql = @"
    SELECT DISTINCT ON (MachineName)
        MachineName, DriveLetter, PercentFree, Timestamp
    FROM RawDriveRows
    WHERE PercentFree < 20
    ORDER BY MachineName, Timestamp DESC;
";
```

### Monitor Specific Machine History
```csharp
// Get all reports for a machine in last 7 days
var sql = @"
    SELECT * FROM RawDriveRows
    WHERE MachineName = @machine
        AND Timestamp >= datetime('now', '-7 days')
    ORDER BY Timestamp DESC;
";
```

---

## Removed Features

### ❌ Query-String Endpoints
The following endpoints have been **removed**:
- `GET /api/server/drive?drive=C:`
- `GET /api/server/space?drive=C:`

### ❌ Single-Row Ingestion
Agents **must** send batches using the new format.

### ❌ Data Transformation at Ingestion
No aggregation, normalization, or calculated fields at insert time.

### ❌ Multiple Databases
Only one SQLite database is used.

---

## Testing

### Test Setup Example
```csharp
var factory = await TestDatabaseFactory.CreateAsync();
var ingestionService = factory.GetIngestionService();
var options = factory.GetOptions();

// In-memory SQLite database for testing
// Automatically cleaned up after test
```

### Writing New Tests
```csharp
[Fact]
public async Task IngestRawRows_WithValidBatch_PersistsToDatabase()
{
    await using var factory = await TestDatabaseFactory.CreateAsync();
    var service = factory.GetIngestionService();

    var rows = new List<RawDriveRow>
    {
        new RawDriveRow
        {
            DriveLetter = "C:",
            TotalSpaceGb = 500,
            UsedSpaceGb = 250,
            FreeSpaceGb = 250,
            PercentFree = 50,
            Timestamp = DateTime.UtcNow
        }
    };

    await service.IngestRawRowsAsync("TestMachine", rows);

    // Verify in database...
}
```

---

## Troubleshooting

### Database File Not Created
- Ensure `C:\ProgramData\StorageWatch\` directory exists and has write permissions
- Check application startup logs for errors
- Verify `ServerOptions.DatabasePath` is correct

### POST /api/agent/report Returns 400
Check request format:
- `machineName` is non-empty string
- `rows` is an array (not null)
- Each row has `driveLetter` property
- All numeric values are valid doubles

### No Data Appearing in Reports
- Verify POST request was successful (check HTTP response)
- Check that `MachineName` in request matches what's queried
- Verify `Timestamp` in rows is valid DateTime

---

## Migration from Old Architecture

### Changes Required in Agent
Update CentralPublisher to send batches in new format:
```json
{
  "machineName": "...",
  "rows": [...]
}
```

### No Data Migration
Old data is not automatically migrated. If needed:
1. Export old data from legacy databases
2. Transform to RawDriveRow format
3. Use RawRowIngestionService to load

### Database Location Change
- **Old:** Relative path `Data/StorageWatchServer.db`
- **New:** `C:\ProgramData\StorageWatch\Server\StorageWatchServer.db`

---

## Performance Notes

### Index Strategy
- Primary index on (MachineName, Timestamp DESC)
- Enables efficient queries for:
  - Recent reports by machine
  - Machine history within date range
  - Latest report per machine

### Batch Insertion
- Use transactions for bulk inserts
- Reduces I/O overhead
- Improves database performance

### Query Optimization Tips
```csharp
// ✅ Good - uses index
SELECT * FROM RawDriveRows 
WHERE MachineName = 'COMPUTER' 
ORDER BY Timestamp DESC LIMIT 100;

// ❌ Bad - full scan
SELECT * FROM RawDriveRows 
WHERE DriveLetter = 'C:' 
ORDER BY Timestamp DESC;
```

---

## Future Enhancements

### Potential Features
1. Data aggregation views (daily summaries)
2. Alerting based on raw data thresholds
3. Data retention policies
4. Compression of old rows
5. Export/backup functionality
6. Trend analysis queries

### Design Considerations
- Keep raw data pristine
- Build views/aggregates separately
- Use stored procedures or scheduled jobs for transformations
- Consider data warehouse for analytics
