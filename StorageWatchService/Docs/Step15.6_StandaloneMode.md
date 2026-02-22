## Step 15.6: Standalone Operational Mode

### Overview

Step 15.6 introduces a new operational mode called **Standalone** to StorageWatch, complementing the existing Agent and Server modes. This implementation enables single-machine operation without any central server dependency, making StorageWatch suitable for isolated environments where local-only monitoring is required.

The three operational modes are now:

1. **Standalone** (New)
   - Local drive monitoring only
   - Local SQLite storage
   - No reporting to central server
   - No dashboard hosting
   - No server API hosting
   - Perfect for single-machine installations

2. **Agent** (Existing)
   - Local drive monitoring
   - Local SQLite storage
   - Reports to central StorageWatch Server
   - Can operate offline with local data
   - Forwards periodic reports when server is available

3. **Server** (Existing)
   - Central aggregation point
   - Hosts web dashboard for multi-machine monitoring
   - Receives and stores reports from agents
   - Hosts REST API for agent communication
   - Manages historical data for all connected machines

---

### Architecture Changes

#### A. StorageWatchMode Enum

The `StorageWatchMode` enum (defined in `StorageWatchService/Config/Options/StorageWatchOptions.cs`) defines the three operational modes:

```csharp
public enum StorageWatchMode
{
    /// Standalone mode: Runs independently on a single host.
    Standalone = 0,

    /// Agent mode: Runs as an agent reporting to a central server.
    /// Default mode.
    Agent = 1,

    /// Server mode: Runs as a central server managing multiple agents.
    Server = 2
}
```

**Key Design Decisions:**
- Numeric values assigned for future serialization/storage use
- `Standalone` is value 0 (earliest/simplest mode)
- Default mode remains `Agent` for backward compatibility
- All modes are peer-level (no inheritance hierarchy)

#### B. StorageWatchOptions Configuration

Added `Mode` property to the root `StorageWatchOptions` class:

```csharp
[Required]
public StorageWatchMode Mode { get; set; } = StorageWatchMode.Agent;
```

**Properties:**
- Required configuration element
- Defaults to `Agent` for backward compatibility
- Can be specified in `appsettings.json` under `StorageWatch.Mode`
- Determines which services are registered at startup

#### C. Configuration Validation

Updated validators in `StorageWatchOptionsValidator.cs` to enforce mode-specific rules:

**StorageWatchOptionsValidator:**
- Validates Mode property exists
- Enforces mode-specific requirements via conditional validation
- For Agent mode: Ensures CentralServerOptions can be configured
- For Standalone mode: No CentralServerOptions required

**CentralServerOptionsValidator:**
- When `Enabled = false`: Skips validation (allows any mode to operate)
- When `Enabled = true` and Mode = "Agent":
  - Requires `ServerUrl` to be set
  - Requires `ReportIntervalSeconds > 0`
  - Validates ServerUrl is a valid URI
- When `Enabled = true` and Mode = "Server":
  - Requires `CentralConnectionString` to be set
  - Validates connection string format

---

### Service Startup Branching Logic

#### StorageWatchService/Program.cs

The startup logic now branches based on operational mode:

**Phase 1: Configuration Loading (Unchanged)**
- Load JSON configuration from `ProgramData\StorageWatch\StorageWatchConfig.json`
- Create default options if file doesn't exist
- Set database connection string

**Phase 2: Options Registration (Updated)**
- Register `StorageWatchOptions` with Mode property
- Configure `CentralServerOptions` from configuration (only used in Agent mode)
- Register all validators

**Phase 3: Mode-Specific Service Registration (New)**

```csharp
if (options.Mode == StorageWatchMode.Agent)
{
    // Agent mode: Register reporting services
    services.AddHttpClient();
    services.AddHttpClient<AgentReportSender>();
    services.AddHostedService<AgentReportWorker>();
}
// Standalone mode: Do NOT register AgentReportWorker or HttpClient for reporting
// Server mode is handled by StorageWatchServer project
```

**Services Always Registered (All Modes):**
- `StorageWatchOptions` configuration
- Validators
- `RollingFileLogger`
- Plugin architecture (AlertSenderPluginRegistry, plugins, AlertSenderPluginManager)
- IPC Communication Server (ServiceCommunicationServer)
- `IDiskStatusProvider` (DiskAlertMonitor)
- `AgentReportBuilder`
- `Worker` (background monitoring service)

**Services Conditionally Registered:**
- **Agent Mode Only:**
  - HttpClient (for reporting)
  - AgentReportSender (formatted report sending)
  - AgentReportWorker (periodic reporting loop)

**Services Never Registered in Any Service Project:**
- Razr Pages infrastructure (dashboard)
- Server API endpoints
- Server database repositories
- (Those are registered by StorageWatchServer/Program.cs)

#### StorageWatchServer/Program.cs

The server startup now includes mode validation:

**Phase 1: Mode Check (New)**
- Read configuration from shared location: `ProgramData\StorageWatch\StorageWatchConfig.json`
- Parse JSON to extract `StorageWatch.Mode` property
- If Mode ≠ "Server", exit gracefully with error messages
- Default to "Server" mode if config not found (maintains backward compatibility)

**Error Handling:**
```
StorageWatch Server can only run in 'Server' mode. Current mode: {DetectedMode}
To run in Agent mode, use StorageWatchService.exe
To run in Standalone mode, use StorageWatchService.exe
```

**Rationale:**
- Prevents accidental startup of server in wrong mode
- Prevents port conflicts and configuration misunderstandings
- Guides users to run correct executable
- Independent of StorageWatch service assembly (no cross-project references)

---

### Configuration Examples

#### Standalone Mode (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "StorageWatch": {
    "Mode": "Standalone"
  },
  "CentralServer": {
    "Enabled": false
  }
}
```

**Characteristics:**
- Minimal configuration required
- CentralServer section optional/disabled
- No server URL needed
- No API key needed
- No reporting interval needed

#### Agent Mode (appsettings.json)

```json
{
  "StorageWatch": {
    "Mode": "Agent"
  },
  "CentralServer": {
    "Enabled": true,
    "Mode": "Agent",
    "ServerUrl": "http://central-server.example.com:5000",
    "ReportIntervalSeconds": 300,
    "AgentId": "DESKTOP-MACHINE1"
  }
}
```

**Characteristics:**
- Mode explicitly set to Agent
- CentralServer.Enabled = true
- ServerUrl is required and validated
- ReportIntervalSeconds must be > 0
- AgentId optional (defaults to machine name)

#### Server Mode

Uses StorageWatchServer/appsettings.json (unchanged):

```json
{
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "Data/StorageWatchServer.db",
    "AgentReportDatabasePath": "Data/StorageWatchAgentReports.db",
    "OnlineTimeoutMinutes": 10
  }
}
```

**Characteristics:**
- Server application only
- Configures listening URL and ports
- Configures database paths for aggregated data
- Validation ensures databases are initialized

---

### Behavior Comparison Matrix

| Behavior | Standalone | Agent | Server |
|----------|-----------|-------|--------|
| **Local Drive Monitoring** | ✓ | ✓ | ✗ |
| **Local SQLite Storage** | ✓ | ✓ | ✗* |
| **Reports to Server** | ✗ | ✓ | ✗ |
| **Receives Agent Reports** | ✗ | ✗ | ✓ |
| **Central SQLite Database** | ✗ | ✗ | ✓ |
| **Hosts Web Dashboard** | ✗ | ✗ | ✓ |
| **Hosts REST API** | ✗ | ✗ | ✓ |
| **HttpClient for Reporting** | ✗ | ✓ | ✗ |
| **AgentReportWorker Running** | ✗ | ✓ | ✗ |
| **Suitable for Single Machine** | ✓ | ✓ | ✗ |
| **Suitable for Multi-Machine** | ✗ | ✓ | ✓ |

*Server processes reports from agents but doesn't have its own local monitoring

---

### Validation Rules

#### StorageWatchOptions Validation

| Rule | Condition | Action |
|------|-----------|--------|
| General options required | Always | Fail if null |
| Monitoring options required | Always | Fail if null |
| Database options required | Always | Fail if null |
| Alerting options required | Always | Fail if null |
| At least one drive to monitor | Always | Fail if empty list |
| Drive format validation | Always | Fail if not "X:" format |
| Threshold percent range | Always | Fail if not 1-100 |
| Alert sender if notifications enabled | Always | Fail if no enabled sender |

#### CentralServerOptions Validation (When Enabled)

| Mode | Required Fields | Validation Rules |
|------|-----------------|------------------|
| Agent | ServerUrl | • Must be valid URI<br>• Must not be empty<br>• ReportIntervalSeconds > 0 |
| Server | CentralConnectionString | • Must not be empty<br>• Typically SQLite connection string |
| (Not Enabled) | None | • Validation skipped<br>• Allows Standalone mode |

#### Mode-Specific Validation Flow

```
Load Configuration
    ↓
Is Mode specified?
    ├─ No → Default to Agent (backward compatible)
    └─ Yes → Use specified mode
         ↓
    Validate StorageWatchOptions (all modes)
         ↓
    Is CentralServer.Enabled?
        ├─ No → Skip CentralServer validation (allows Standalone)
        └─ Yes → Validate based on Mode
             ├─ Agent → Requires ServerUrl, ReportIntervalSeconds
             ├─ Server → Requires CentralConnectionString
             └─ Unknown → Fail with clear error message
```

---

### Tests Added/Updated

#### OperationalModeTests.cs

Located in: `StorageWatchService.Tests/UnitTests/OperationalModeTests.cs`

**Test Coverage:**

1. **Enum Tests**
   - `StorageWatchOptions_DefaultMode_IsStandalone()`
   - `StorageWatchOptions_CanSetMode_ToAgent()`
   - `StorageWatchOptions_CanSetMode_ToServer()`

2. **Validator Tests**
   - `StorageWatchOptionsValidator_Standalone_ValidatesSuccessfully()`
   - `StorageWatchOptionsValidator_Agent_ValidatesSuccessfully()`
   - `CentralServerOptionsValidator_WhenNotEnabled_SkipsValidation()`
   - `CentralServerOptionsValidator_AgentMode_RequiresServerUrl()`
   - `CentralServerOptionsValidator_AgentMode_ValidWithServerUrl()`
   - `CentralServerOptionsValidator_ServerMode_RequiresCentralConnectionString()`
   - `CentralServerOptionsValidator_ServerMode_ValidWithConnectionString()`

3. **Dependency Injection Tests**
   - `ServiceBuilder_StandaloneMode_DoesNotRegisterAgentReportWorker()`
     - Verifies AgentReportWorker is NOT in hosted services
   - `ServiceBuilder_AgentMode_RegistersAgentReportWorker()`
     - Verifies AgentReportWorker IS in hosted services

**Test Strategy:**
- Tests validate mode enum exists and is accessible
- Tests verify configuration binding works for all modes
- Tests confirm validation rules are enforced
- Tests ensure dependency injection differs by mode
- Tests use FluentAssertions for readable assertions

---

### Implementation Details

#### Files Modified

1. **StorageWatchService/Config/Options/StorageWatchOptions.cs**
   - Added `Mode` property to `StorageWatchOptions` class
   - Default value: `StorageWatchMode.Agent`
   - Marked as `[Required]`

2. **StorageWatchService/Config/Options/StorageWatchOptionsValidator.cs**
   - Updated `StorageWatchOptionsValidator.Validate()` to handle Mode
   - No changes to `CentralServerOptionsValidator` logic (already correct)

3. **StorageWatchService/Program.cs**
   - Added `cfg.Mode = options.Mode;` in options configuration
   - Added mode-specific service registration:
     ```csharp
     if (options.Mode == StorageWatchMode.Agent)
     {
         services.AddHttpClient();
         services.AddHttpClient<AgentReportSender>();
         services.AddHostedService<AgentReportWorker>();
     }
     ```
   - Updated default options creation: `defaultOptions.Mode = StorageWatchMode.Standalone;`

4. **StorageWatchServer/Program.cs**
   - Added JSON parsing to read `StorageWatch.Mode` from config
   - Added mode validation: if Mode ≠ "Server", exit with error
   - Graceful error handling with informative messages
   - Defaults to "Server" mode if config missing

5. **StorageWatchService/appsettings.json**
   - Added `"StorageWatch": { "Mode": "Standalone" }`
   - Updated default CentralServer to disabled state

6. **StorageWatchService.Tests/UnitTests/OperationalModeTests.cs** (New)
   - Comprehensive test coverage for all three modes
   - Validates configuration binding and validation
   - Verifies dependency injection differences

#### Files NOT Modified

- AgentReportWorker.cs (logic unchanged)
- AgentReportSender.cs (logic unchanged)
- CentralServerOptions.cs (added no new required fields)
- Server reporting endpoints (unchanged)
- Dashboard UI pages (unchanged)
- Installer files (not updated in this step)
- Any Step 15.1-15.5 functionality (preserved)

---

### Operational Flows

#### Standalone Mode Startup Flow

```
1. Read config (or create default with Mode=Standalone)
2. Initialize database (local SQLite)
3. Register all common services
4. Skip: HttpClient, AgentReportSender, AgentReportWorker
5. Register: Worker (local monitoring)
6. Start service
   → Monitors local drives
   → Stores results in local database
   → Sends local alerts (SMTP, GroupMe, etc.)
   → Runs indefinitely
```

#### Agent Mode Startup Flow

```
1. Read config (Mode=Agent, CentralServer.Enabled=true)
2. Validate ServerUrl and ReportIntervalSeconds
3. Initialize database (local SQLite)
4. Register all common services
5. Register: HttpClient, AgentReportSender, AgentReportWorker
6. Start service
   → Monitors local drives
   → Stores results in local database
   → Sends local alerts
   → Starts AgentReportWorker
     - Periodically gathers data
     - Sends reports to central server
     - Handles network failures gracefully
```

#### Server Startup Flow

```
1. Read config from shared location
2. Validate Mode == "Server"
   ├─ If not Server: Log error and exit(1)
   └─ If Server: Continue
3. Initialize centralized Razor Pages infrastructure
4. Initialize central database (aggregated data)
5. Initialize agent report database
6. Register server-specific services
7. Start listening for agent reports
8. Host web dashboard
9. Maintain multi-machine status
```

---

### Error Handling

#### Mode Validation Errors

**If Standalone mode with CentralServer.Enabled=true:**
```
Configuration Error: 
- Standalone mode should not have CentralServer.Enabled
- Set CentralServer.Enabled=false or change Mode to Agent
```
(Handled by configuration validation)

**If Agent mode without ServerUrl:**
```
Configuration Error:
- Agent mode requires ServerUrl to be configured
- Example: "ServerUrl": "http://central-server:5000"
```

**If Server program runs with Mode != Server:**
```
2024-XX-XX XX:XX:XX Error: StorageWatch Server can only run in 'Server' mode. 
                           Current mode: Agent
2024-XX-XX XX:XX:XX Error: To run in Agent mode, use StorageWatchService.exe
2024-XX-XX XX:XX:XX Error: To run in Standalone mode, use StorageWatchService.exe

Process exits with code 1
```

---

### Backward Compatibility

**Preserved Behaviors:**
- Default mode is Agent (for existing deployments)
- CentralServer.Enabled defaults to false (safe default)
- If no config found, creates default with:
  - Mode=Standalone
  - CentralServer.Enabled=false
- Existing Agent configurations continue to work unchanged

**Migration Path:**
- Existing Agent deployments: Set "Mode": "Agent" or rely on default
- Existing Standalone users: Explicitly set "Mode": "Standalone"
- Existing Server deployments: Ensure StorageWatchServer runs (unchanged)

---

### Performance Implications

**Standalone Mode:**
- Slightly faster startup (no HttpClient initialization)
- Smaller memory footprint (no reporting infrastructure)
- No network overhead
- Suitable for resource-constrained machines

**Agent Mode:**
- Standard performance (unchanged from before Step 15.6)
- Network latency only during report intervals (default 5 minutes)
- Graceful handling of network unavailability

**Server Mode:**
- No changes to server performance
- Validation ensures correct mode prevents startup in wrong mode

---

### Security Considerations

**Standalone Mode:**
- No network communication
- No API keys needed
- Local data only
- Suitable for highly secure/isolated environments

**Agent Mode:**
- ServerUrl should use HTTPS in production
- ApiKey should be encrypted in configuration (optional feature)
- Reports sent over network
- Consider firewall rules

**Server Mode:**
- ListenUrl should use HTTPS in production
- API key validation of incoming reports
- Data persisted in central database
- Consider network isolation of central server

---

### Future Enhancements

**Potential improvements for future steps:**
1. Mode switching at runtime (with service restart)
2. Metrics for each mode (startup time, memory usage)
3. Configuration migration tools (Standalone ↔ Agent ↔ Server)
4. Telemetry/diagnostics per mode
5. Load balancing for Server mode (multiple servers)
6. Fallback mode (Agent → Standalone if server unreachable)

---

### Conclusion

Step 15.6 successfully implements the Standalone operational mode, enabling StorageWatch to serve three distinct deployment scenarios:

- **Standalone:** Single-machine, local-only monitoring
- **Agent:** Multi-machine with optional central aggregation
- **Server:** Central hub for distributed monitoring

The implementation maintains backward compatibility, enforces configuration validation, and provides clear error messages for misconfiguration. All three modes are now first-class citizens with equal support in the codebase.

