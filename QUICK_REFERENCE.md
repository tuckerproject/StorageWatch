# StorageWatch Server - Quick Reference Card

## 🎯 New API Endpoint

### POST /api/agent/report
```bash
curl -X POST http://localhost:5001/api/agent/report \
  -H "Content-Type: application/json" \
  -d '{
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
  }'
```

### Responses
| Status | Meaning |
|--------|---------|
| 200 | ✅ Batch accepted and processed |
| 400 | ❌ Invalid request format |
| 500 | ❌ Server error |

---

## 📦 Database

### Location
```
C:\ProgramData\StorageWatch\Server\StorageWatchServer.db
```

### Key Table
```sql
CREATE TABLE RawDriveRows (
    Id INTEGER PRIMARY KEY,
    MachineName TEXT NOT NULL,
    DriveLetter TEXT NOT NULL,
    TotalSpaceGb REAL NOT NULL,
    UsedSpaceGb REAL NOT NULL,
    FreeSpaceGb REAL NOT NULL,
    PercentFree REAL NOT NULL,
    Timestamp DATETIME NOT NULL
);
```

### Sample Query
```sql
-- Get latest report per machine
SELECT DISTINCT MachineName, MAX(Timestamp) as LatestTime
FROM RawDriveRows
GROUP BY MachineName
ORDER BY LatestTime DESC
LIMIT 50;

-- Get all drives from latest report for a machine
SELECT * FROM RawDriveRows
WHERE MachineName = 'COMPUTER-01'
  AND Timestamp = (
    SELECT MAX(Timestamp) FROM RawDriveRows 
    WHERE MachineName = 'COMPUTER-01'
  )
ORDER BY DriveLetter;
```

---

## 🔧 Configuration

### File Location
```
C:\ProgramData\StorageWatch\Server\ServerConfig.json
```

### Default Settings
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

## 🏗️ Architecture at a Glance

```
Agent (CentralPublisher)
    ↓
    POST /api/agent/report
    ↓
RawRowsController
    ↓
RawRowIngestionService
    ↓
SQLite Database (RawDriveRows table)
    ↓
Reports Dashboard
```

---

## 📋 Validation Rules

### machineName
- ✅ Required
- ✅ Non-empty string
- ✅ Examples: "COMPUTER-01", "SERVER-PROD-01"

### rows
- ✅ Required array
- ✅ Must have at least 1 item
- ✅ Each row must have driveLetter

### Row Properties
| Property | Type | Required | Example |
|----------|------|----------|---------|
| driveLetter | string | ✅ | "C:" |
| totalSpaceGb | number | ✅ | 500.0 |
| usedSpaceGb | number | ✅ | 250.0 |
| freeSpaceGb | number | ✅ | 250.0 |
| percentFree | number | ✅ | 50.0 |
| timestamp | datetime | ✅ | "2024-01-15T10:30:00Z" |

---

## 🗂️ Project Structure

```
StorageWatchServer/
├── Controllers/
│   ├── HealthController.cs
│   ├── ServerController.cs
│   └── RawRowsController.cs ⭐ NEW
├── Server/
│   ├── Api/
│   │   ├── AgentReportRequest.cs (UPDATED)
│   │   └── ApiEndpoints.cs (UPDATED)
│   ├── Data/
│   │   ├── ServerRepository.cs
│   │   └── ServerSchema.cs (UPDATED)
│   ├── Models/
│   │   ├── RawDriveRow.cs ⭐ NEW
│   │   └── ... other models
│   ├── Reporting/
│   │   └── RawRowIngestionService.cs ⭐ NEW
│   └── Services/
│       └── ServerOptions.cs (UPDATED)
├── Dashboard/
│   └── Reports.cshtml.cs (UPDATED)
└── Program.cs (UPDATED)
```

---

## 🧪 Testing

### Run Tests
```bash
dotnet test StorageWatchServer.Tests
```

### Test Database
```csharp
var factory = await TestDatabaseFactory.CreateAsync();
var service = factory.GetIngestionService();
```

### Create Test Data
```csharp
var request = TestDataFactory.CreateAgentReport("TestMachine", 2);
var row = TestDataFactory.CreateRawDriveRow();
```

---

## ⚡ Common Operations

### Check Database Size
```powershell
(ls "C:\ProgramData\StorageWatch\Server\StorageWatchServer.db").Length
```

### Query Row Count
```bash
sqlite3 "C:\ProgramData\StorageWatch\Server\StorageWatchServer.db" \
  "SELECT COUNT(*) FROM RawDriveRows;"
```

### Get Latest Timestamp
```bash
sqlite3 "C:\ProgramData\StorageWatch\Server\StorageWatchServer.db" \
  "SELECT MAX(Timestamp) FROM RawDriveRows;"
```

### List All Machines
```bash
sqlite3 "C:\ProgramData\StorageWatch\Server\StorageWatchServer.db" \
  "SELECT DISTINCT MachineName FROM RawDriveRows ORDER BY MachineName;"
```

---

## 🚨 Troubleshooting

### Problem: 400 Bad Request
**Causes:**
- machineName is empty or missing
- rows array is empty or null
- Row missing driveLetter

**Solution:** Check JSON format matches example above

### Problem: Database File Not Created
**Causes:**
- No write permissions to C:\ProgramData\StorageWatch\
- Server crashed during startup

**Solution:**
1. Check C:\ProgramData\StorageWatch\ exists and is writable
2. Check application logs for errors
3. Restart application

### Problem: No Data in Dashboard
**Causes:**
- POST request failed (check HTTP response)
- Wrong machine name queried
- No reports sent yet

**Solution:**
1. Verify POST returned 200
2. Check database directly: `SELECT * FROM RawDriveRows LIMIT 1;`
3. Verify agents are configured to send reports

---

## 📦 Dependencies

### NuGet Packages (Key)
- Microsoft.Data.Sqlite 10.0.0
- Microsoft.AspNetCore.* (ASP.NET Core)
- Microsoft.Extensions.* (DI, Logging, etc.)

### Database
- SQLite (file-based, no server required)

---

## 🔗 Key Files Reference

| File | Purpose | Status |
|------|---------|--------|
| RawRowsController.cs | API endpoint | ⭐ NEW |
| RawRowIngestionService.cs | Batch ingestion | ⭐ NEW |
| RawDriveRow.cs | Data model | ⭐ NEW |
| ServerSchema.cs | DB initialization | UPDATED |
| AgentReportRequest.cs | Request DTO | UPDATED |
| ServerOptions.cs | Configuration | UPDATED |
| Reports.cshtml.cs | Dashboard | UPDATED |
| Program.cs | DI & startup | UPDATED |

---

## 📊 Data Flow Example

```
Agent sends:
{
  "machineName": "OFFICE-PC-01",
  "rows": [
    { "driveLetter": "C:", "totalSpaceGb": 500, ... },
    { "driveLetter": "D:", "totalSpaceGb": 1000, ... }
  ]
}

↓

RawRowsController validates and converts to:
List<RawDriveRow> {
  { DriveLetter: "C:", MachineName: "OFFICE-PC-01", ... },
  { DriveLetter: "D:", MachineName: "OFFICE-PC-01", ... }
}

↓

RawRowIngestionService inserts into:
RawDriveRows table (2 rows)

↓

Reports page displays:
Machine: OFFICE-PC-01
  C: 500 GB (250 GB free)
  D: 1000 GB (500 GB free)
```

---

## 🎓 Learning Resources

- **Architecture Guide:** See ARCHITECTURE_GUIDE.md
- **Implementation Details:** See IMPLEMENTATION_SUMMARY.md
- **Change Details:** See CHANGE_LOG.md
- **Code Examples:** Check RawRowsController.cs and RawRowIngestionService.cs

---

## ✅ Checklist Before Deploy

- [ ] Build succeeds (dotnet build)
- [ ] Tests compile (dotnet test with skipped tests OK)
- [ ] Database path is writable
- [ ] Agents updated to use new endpoint
- [ ] POST /api/agent/report tested with curl
- [ ] Dashboard loads without errors
- [ ] Batch insertions working (check database)

---

**Version:** 1.0  
**Last Updated:** 2024-01-15  
**Status:** ✅ Implementation Complete
