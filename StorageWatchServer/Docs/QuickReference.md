# StorageWatchServer — Quick Reference Guide

**Fast lookup for common tasks and API usage**

---

## Configuration

### Listen on Different Port
```json
{
  "Server": {
    "ListenUrl": "http://0.0.0.0:8080"
  }
}
```

### Change Database Location
```json
{
  "Server": {
    "DatabasePath": "C:\\monitoring\\agents.db"
  }
}
```

### Change Online Timeout
```json
{
  "Server": {
    "OnlineTimeoutMinutes": 5
  }
}
```

---

## API Quick Reference

### Post Agent Report
```bash
curl -X POST http://localhost:5001/api/agent/report \
  -H "Content-Type: application/json" \
  -d '{
    "machineName": "LAPTOP-ABC",
    "collectionTimeUtc": "2024-01-15T14:30:00Z",
    "drives": [
      {
        "driveLetter": "C:",
        "totalSpaceGb": 500,
        "usedSpaceGb": 350,
        "freeSpaceGb": 150,
        "percentFree": 30,
        "collectionTimeUtc": "2024-01-15T14:30:00Z"
      }
    ]
  }'
```

### Get All Machines
```bash
curl http://localhost:5001/api/machines
```

### Get Machine Details
```bash
curl http://localhost:5001/api/machines/1
```

### Get Drive History (Last 7 Days)
```bash
curl "http://localhost:5001/api/machines/1/history?drive=C:&range=7d"
```

### Get History (Last 24 Hours)
```bash
curl "http://localhost:5001/api/machines/1/history?drive=C:&range=24h"
```

### Get Alerts
```bash
curl http://localhost:5001/api/alerts
```

### Get Settings
```bash
curl http://localhost:5001/api/settings
```

---

## Dashboard Routes

| URL | Page | Purpose |
|-----|------|---------|
| `/` | Index | Machine list overview |
| `/machines/1` | Details | Machine details + charts |
| `/alerts` | Alerts | Alert management |
| `/settings` | Settings | Configuration view |

---

## Database Schema (Key Tables)

### Machines
```sql
SELECT * FROM Machines;
-- Columns: Id, MachineName, LastSeenUtc, CreatedUtc
```

### Current Drive Status
```sql
SELECT * FROM MachineDrives WHERE MachineId = 1;
-- Columns: Id, MachineId, DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, LastSeenUtc
```

### Historical Data
```sql
SELECT * FROM DiskHistory 
WHERE MachineId = 1 AND DriveLetter = 'C:' 
AND CollectionTimeUtc >= datetime('now', '-7 days');
-- Columns: Id, MachineId, DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, CollectionTimeUtc
```

### Alerts
```sql
SELECT * FROM Alerts WHERE IsActive = 1;
-- Columns: Id, MachineId, Severity, Message, CreatedUtc, ResolvedUtc, IsActive
```

---

## C# Examples

### Check If Machine is Online
```csharp
var statusService = serviceProvider.GetRequiredService<MachineStatusService>();
bool isOnline = statusService.IsOnline(machine.LastSeenUtc);
```

### Get All Machines via Repository
```csharp
var repository = serviceProvider.GetRequiredService<ServerRepository>();
var machines = await repository.GetMachinesAsync();
```

### Get Machine History
```csharp
var history = await repository.GetDiskHistoryAsync(
    machineId: 1,
    driveLetter: "C:",
    startUtc: DateTime.UtcNow.AddDays(-7)
);
```

### Insert Disk History Entry
```csharp
var point = new DiskHistoryPoint
{
    CollectionTimeUtc = DateTime.UtcNow,
    TotalSpaceGb = 500,
    UsedSpaceGb = 350,
    FreeSpaceGb = 150,
    PercentFree = 30
};
await repository.InsertDiskHistoryAsync(machineId: 1, driveLetter: "C:", point);
```

---

## Common Tasks

### Clear All Data
1. Stop the server
2. Delete `Data/StorageWatchServer.db`
3. Restart the server (database will be recreated)

### Check Server Logs
On Windows:
```powershell
Get-Content ./logs/application.log -Tail 50
```

On Linux/Mac:
```bash
tail -50 ./logs/application.log
```

### Verify Database Integrity
```bash
sqlite3 Data/StorageWatchServer.db "PRAGMA integrity_check;"
```

### Count Records in Database
```bash
sqlite3 Data/StorageWatchServer.db "SELECT COUNT(*) FROM DiskHistory;"
```

---

## Debugging

### Enable Debug Logging
In appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Test API Endpoint
```bash
# Windows
Invoke-WebRequest -Uri http://localhost:5001/api/machines

# Linux/Mac
curl http://localhost:5001/api/machines | json_pp
```

### Check Port Availability
```bash
# Windows
netstat -ano | findstr :5001

# Linux/Mac
lsof -i :5001
```

---

## Troubleshooting

### "Address already in use" Error
The port is in use by another process.
```bash
# Kill process on Windows
netstat -ano | findstr :5001
taskkill /PID <PID> /F

# Kill process on Linux
lsof -ti:5001 | xargs kill -9
```

### Database Locked Error
Wait a moment and retry. If persistent:
1. Stop the server
2. Delete `Data/StorageWatchServer.db-wal` and `-shm` files
3. Restart

### No Data Showing in Dashboard
1. Verify agents are sending reports: `GET /api/machines`
2. Check server logs for errors
3. Verify agent machine names match in reports

---

## Testing

### Run All Tests
```bash
dotnet test StorageWatchServer.Tests
```

### Run Specific Test Class
```bash
dotnet test --filter FullyQualifiedName~MachineStatusServiceTests
```

### Run with Coverage Report
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura
```

---

## Performance Tips

### Query Optimization
- Always filter by date range when querying history
- Use appropriate indexes (already created)
- Archive old data periodically (future feature)

### Cleanup Old Data (Manual)
```sql
DELETE FROM DiskHistory 
WHERE CollectionTimeUtc < datetime('now', '-30 days');
```

### Monitor Database Size
```bash
# Windows
dir Data\StorageWatchServer.db

# Linux/Mac
ls -lh Data/StorageWatchServer.db
```

---

## Environment Variables

```bash
# Override configuration via environment
set Server__ListenUrl=http://0.0.0.0:8080
set Server__DatabasePath=C:\data\agents.db
set Server__OnlineTimeoutMinutes=5
```

---

## Project Structure

```
StorageWatchServer/
├── Program.cs                    # Startup
├── Server/Api/ApiEndpoints.cs    # REST routes
├── Server/Data/                  # Database
├── Server/Models/                # Data classes
├── Server/Services/              # Business logic
├── Dashboard/                    # Razor pages
├── wwwroot/css/                  # Styling
└── Docs/                         # Documentation
```

---

## Version Info

- **Runtime**: .NET 10
- **Web Framework**: ASP.NET Core 10
- **Database**: SQLite 3
- **Chart Library**: Chart.js 4.4.1

---

**For complete documentation, see [CentralWebDashboard.md](./CentralWebDashboard.md)**
