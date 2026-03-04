# StorageWatch Agent Architecture Refactoring

## Summary

Successfully refactored the StorageWatchAgent to restore a single reporting pipeline with the following changes:

## 1. New CentralPublisher Component

**File:** `StorageWatchAgent/Services/CentralServer/CentralPublisher.cs`

- Implements a fixed-interval publishing mechanism (default: 300 seconds)
- Loads `last_central_run.txt` to track the last successful publish timestamp
- Queries SQLite for all raw drive rows where `Timestamp > last_central_run`
- Batches rows into groups of 100
- POSTs each batch to Central Server endpoint: `POST /api/agent/report`
- Request body format:
  ```json
  {
    "machineName": "<Environment.MachineName>",
    "rows": [ ...raw drive rows... ]
  }
  ```
- Updates `last_central_run.txt` after each successful batch
- Implements offline/online state tracking:
  - Logs "Central server unreachable… entering offline mode" once when server becomes unavailable
  - Suppresses further errors until server becomes reachable
  - Logs "Central server reachable… resuming publishing" when connection is restored
- Silent operation when no new rows exist

## 2. Simplified CentralServerOptions

**File:** `StorageWatchAgent/Config/Options/CentralServerOptions.cs`

**Removed fields:**
- `Enabled`
- `Mode`
- `AgentId`
- `Port`
- `CentralConnectionString`
- `ServerId`
- `ReportIntervalSeconds`

**Kept fields:**
- `ServerUrl` (required)
- `CheckIntervalSeconds` (default: 300)
- `ApiKey` (optional)

## 3. Updated StorageWatchMode Enum

**File:** `StorageWatchAgent/Config/Options/StorageWatchOptions.cs`

**Removed:**
- `Server` mode

**Kept:**
- `Standalone` mode
- `Agent` mode

## 4. Removed Components

### Deleted Files:
- `StorageWatchAgent/Services/CentralServer/AgentReportWorker.cs` - Continuous push loop (replaced by CentralPublisher)

### Deprecated/Stubbed Files:
- `StorageWatchAgent/Services/CentralServer/CentralServerService.cs` - Server-side code (functionality moved to StorageWatchServer project)
- `StorageWatchAgent/Services/CentralServer/CentralServerForwarder.cs` - Legacy forwarder (IsEnabled() now returns false)

### Removed Validators:
- `CentralServerOptionsValidator` - Removed from `StorageWatchOptionsValidator.cs` (validation now via DataAnnotations)

## 5. Updated Program.cs

**File:** `StorageWatchAgent/Program.cs`

Changes:
- Simplified `CentralServerOptions` configuration to only bind `ServerUrl`, `CheckIntervalSeconds`, and `ApiKey`
- Removed `CentralServerOptionsValidator` registration
- Removed `AgentReportBuilder` and `AgentReportSender` registrations
- Replaced `AgentReportWorker` with `CentralPublisher`
- `CentralPublisher` registered only when `Mode == StorageWatchMode.Agent`

## 6. Updated Worker.cs

**File:** `StorageWatchAgent/Services/Worker.cs`

Changes:
- Removed central server forwarder initialization
- Removed server mode initialization logic
- Added using directive for `StorageWatch.Services.DataRetention`
- SqlReporter no longer uses forwarder (publishing handled by CentralPublisher)

## 7. Updated AutoUpdateWorker.cs

**File:** `StorageWatchAgent/Services/AutoUpdate/AutoUpdateWorker.cs`

Changes:
- Removed Server mode check
- Auto-update now runs in both Standalone and Agent modes

## 8. Updated AgentConfig.default.json

**File:** `StorageWatchAgent/Defaults/AgentConfig.default.json`

Updated `CentralServer` section to:
```json
"CentralServer": {
  "ServerUrl": "http://central-server.example.com:5000",
  "CheckIntervalSeconds": 300,
  "ApiKey": ""
}
```

## 9. Updated Test Files

### Modified Tests:
- `StorageWatchAgent.Tests/UnitTests/AutoUpdateTests.cs` - Changed Server mode test to Agent mode
- `StorageWatchAgent.Tests/UnitTests/OperationalModeTests.cs` - Updated tests to use CentralPublisher instead of AgentReportWorker, removed CentralServerOptionsValidator tests
- `StorageWatchAgent.Tests/UnitTests/AgentReportOptionsBindingTests.cs` - Updated to test new simplified CentralServerOptions

## 10. New State File

**Location:** `%ProgramData%\StorageWatch\Agent\last_central_run.txt`

- Tracks the last successful publish timestamp
- Independent from `last_sql_run.txt` (used by SqlReporterScheduler)
- ISO 8601 format (e.g., "2024-01-15T10:30:00.0000000Z")

## Architecture Flow

### Local Collection (Unchanged)
1. `SqlReporterScheduler` runs based on `CollectionTime` (e.g., "02:00")
2. Writes raw drive rows to SQLite
3. Updates `last_sql_run.txt`

### Central Publishing (New)
1. `CentralPublisher` runs every `CheckIntervalSeconds` (default: 300)
2. Loads `last_central_run.txt`
3. Queries SQLite for new rows since last publish
4. Batches rows (100 per batch)
5. POSTs to `{ServerUrl}/api/agent/report`
6. Updates `last_central_run.txt` after each successful batch
7. Handles offline/online states gracefully

### Alerting (Unchanged)
- Remains agent-side
- No changes to alert logic

## Key Benefits

1. **Single Source of Truth**: SQLite is the authoritative local store
2. **Decoupled Collection and Publishing**: Collection runs on schedule, publishing runs independently
3. **Fault Tolerant**: Offline mode prevents log spam, automatic backlog flushing when server returns
4. **Batched Transfers**: Efficient 100-row batches reduce HTTP overhead
5. **Simplified Configuration**: Only 3 settings needed for central publishing
6. **Clear Separation**: Agent project no longer contains server-side code

## Migration Notes

Existing deployments need to update `AgentConfig.json`:

**Old format:**
```json
"CentralServer": {
  "Enabled": false,
  "Mode": "Agent",
  "ServerUrl": "http://central-server.example.com:5000",
  "ApiKey": "",
  "AgentId": "agent-1",
  "ReportIntervalSeconds": 300,
  "Port": 5000,
  "CentralConnectionString": "...",
  "ServerId": "..."
}
```

**New format:**
```json
"CentralServer": {
  "ServerUrl": "http://central-server.example.com:5000",
  "CheckIntervalSeconds": 300,
  "ApiKey": ""
}
```

Publishing is automatically enabled when `Mode` is set to `Agent` and `ServerUrl` is configured.
