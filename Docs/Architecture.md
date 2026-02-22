# StorageWatch — Architecture Overview

This document describes the architecture of the StorageWatch platform, including its components, deployment modes, data flow, and technology stack.

---

## Components

StorageWatch is composed of three deployable components:

```
┌─────────────────────────────────────────────────────────────┐
│                      StorageWatch Platform                   │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────┐  │
│  │ StorageWatchSvc  │  │ StorageWatchSvr  │  │   UI     │  │
│  │  (Windows Svc)   │  │  (Central Svc)   │  │  (WPF)   │  │
│  └──────────────────┘  └──────────────────┘  └──────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### StorageWatchService

A .NET 10 Windows Service that runs on every monitored machine.

**Responsibilities:**
- Continuously monitors configured drives (default: every 60 seconds)
- Evaluates drive state using a three-state machine: `NORMAL`, `ALERT`, `NOT_READY`
- Sends alerts via configured plugins when state changes
- Writes daily disk metrics to a local SQLite database
- Persists alert state across reboots
- Optionally reports data to a central StorageWatchServer (Agent mode)

**Internal components:**

| Class | Role |
|---|---|
| `Worker` | .NET hosted service; runs background loops |
| `NotificationLoop` | Alert monitoring loop with state machine |
| `SqlReporter` | Daily SQLite logging with missed-run recovery |
| `DiskAlertMonitor` | Reads drive metrics from the OS |
| `AlertSenderPluginManager` | Discovers and dispatches to alert plugins |
| `GroupMeAlertSender` | Sends GroupMe bot messages |
| `SmtpAlertSender` | Sends SMTP email alerts |
| `RollingFileLogger` | Log rotation (1 MB, 3 files) |
| `ServiceCommunicationServer` | Named-pipe IPC server for the UI |

### StorageWatchServer

An ASP.NET Core 10 application that acts as a central aggregation server.

**Responsibilities:**
- Receives disk reports from agent machines via REST API
- Stores multi-machine historical data in a local SQLite database
- Detects online/offline machine status
- Hosts a Razor Pages web dashboard
- Manages alert records across all connected machines

**Internal components:**

| Class | Role |
|---|---|
| `ApiEndpoints` | Minimal API routes (`/api/...`) |
| `ServerRepository` | SQLite data access layer |
| `MachineStatusService` | Online/offline detection logic |
| `Dashboard/` | Razor Pages UI (Index, Machines, Alerts, Settings) |

### StorageWatchUI

A .NET 10 WPF desktop application.

**Responsibilities:**
- Displays local disk usage from the local SQLite database
- Displays historical trend charts
- Shows multi-machine status from the central server (when configured)
- Controls the StorageWatchService (start/stop/restart)
- Shows service logs and current configuration

**Internal components:**

| Class | Role |
|---|---|
| `DashboardViewModel` | Local disk status |
| `TrendsViewModel` | Historical charts |
| `CentralViewModel` | Central server machine list |
| `SettingsViewModel` | Configuration display and validation |
| `ServiceStatusViewModel` | Service control and log viewer |
| `LocalDataProvider` | SQLite reads for local data |
| `CentralDataProvider` | REST API client for central server |
| `ServiceCommunicationClient` | Named-pipe IPC client to service |

---

## Deployment Modes

StorageWatch supports three deployment modes, configured via `StorageWatch.Mode` in `appsettings.json`.

### Standalone Mode

```
┌────────────────────────────┐
│   Single Machine           │
│                            │
│  StorageWatchService       │
│  ├── Drive monitoring      │
│  ├── SQLite (local)        │
│  └── Alert plugins         │
│                            │
│  StorageWatchUI            │
│  └── Local SQLite charts   │
└────────────────────────────┘
```

Best for: single-machine environments, home labs.

### Agent + Server Mode

```
┌───────────────────┐    ┌───────────────────────────┐
│   Agent Machine   │    │   Central Server Machine  │
│                   │    │                           │
│  SW Service       │───▶│  StorageWatchServer       │
│  ├── Monitoring   │    │  ├── REST API             │
│  ├── SQLite       │    │  ├── SQLite (multi)       │
│  └── Alerts       │    │  └── Web dashboard        │
│                   │    │                           │
│  SW UI            │    │  SW UI (optional)         │
│  └── Local + API  │    │  └── Full fleet view      │
└───────────────────┘    └───────────────────────────┘
```

Best for: multi-machine environments, server farms.

---

## Data Flow

### Alert Flow (Service)

```
Drive scan
    │
    ▼
DiskAlertMonitor.GetStatus()
    │
    ▼
State machine evaluation
  ├── State unchanged? ──▶ No action
  └── State changed?   ──▶ AlertSenderPluginManager.SendAsync()
                               ├── SmtpAlertSender
                               └── GroupMeAlertSender
```

### Reporting Flow (Service)

```
Daily schedule (CollectionTime)
    │
    ▼
SqlReporter.WriteDailyReportAsync()
    │
    ├── Write to local SQLite (StorageWatch.db)
    │
    └── [Agent mode] POST to /api/agent/report on StorageWatchServer
```

### UI Data Flow

```
StorageWatchUI
    │
    ├── LocalDataProvider ──▶ Local SQLite (StorageWatch.db)
    │
    ├── ServiceCommunicationClient ──▶ Named pipe ──▶ StorageWatchService
    │
    └── CentralDataProvider ──▶ HTTP ──▶ StorageWatchServer /api/...
```

---

## IPC Communication (Service ↔ UI)

The service and UI communicate over a **named pipe** (`StorageWatchServicePipe`) for:
- Real-time service status (uptime, last execution, errors)
- Log access
- Configuration validation
- Plugin health checks
- Alert testing

The UI falls back to direct SQLite and file reads when the service pipe is unavailable.

See [ServiceCommunication/Architecture.md](../StorageWatchService/Docs/ServiceCommunication/Architecture.md) for details.

---

## Database Schemas

### Local Database (StorageWatch.db)

Used by StorageWatchService and StorageWatchUI.

```sql
CREATE TABLE DiskSpaceLog (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineName      TEXT NOT NULL,
    DriveLetter      TEXT NOT NULL,
    TotalSpaceGB     REAL NOT NULL,
    UsedSpaceGB      REAL NOT NULL,
    FreeSpaceGB      REAL NOT NULL,
    PercentFree      REAL NOT NULL,
    CollectionTimeUtc DATETIME NOT NULL,
    CreatedAt        DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_DiskSpaceLog_Machine_Drive_Time
ON DiskSpaceLog(MachineName, DriveLetter, CollectionTimeUtc);
```

### Central Database (StorageWatchServer.db)

Used by StorageWatchServer.

```sql
Machines       -- Registered agent machines
MachineDrives  -- Latest drive status per machine
DiskHistory    -- Time-series disk usage data
Alerts         -- Active and historical alerts
Settings       -- Server configuration (read-only in dashboard)
```

---

## Technology Stack

| Layer | Technology | License |
|---|---|---|
| Runtime | .NET 10 | MIT |
| Service host | .NET Worker Service | MIT |
| Web server | ASP.NET Core 10 / Kestrel | MIT |
| UI framework | WPF (.NET 10) | MIT |
| Database | SQLite (Microsoft.Data.Sqlite) | MIT |
| Charts | LiveChartsCore.SkiaSharpView.WPF | MIT |
| Web charts | Chart.js | MIT |
| Installer | NSIS 3.x | zlib/libpng |

All dependencies are MIT, Public Domain, or similarly permissive.

---

## File and Directory Layout

```
StorageWatch/
├── Docs/                          # Solution-level documentation
├── StorageWatchService/           # Windows Service project
│   ├── Config/                    # Configuration loading and options
│   ├── Data/                      # SQLite schema and repository
│   ├── Services/
│   │   ├── Alerting/              # IAlertSender and plugin implementations
│   │   ├── Monitoring/            # Drive scanning
│   │   └── Scheduling/            # Background loops
│   ├── Communication/             # Named-pipe IPC server
│   └── Docs/                      # Service-level documentation
├── StorageWatchService.Tests/     # Service unit and integration tests
├── StorageWatchServer/            # Central server project
│   ├── Server/
│   │   ├── Api/                   # REST API endpoints
│   │   ├── Data/                  # SQLite repository
│   │   ├── Models/                # DTOs
│   │   └── Services/              # Business logic
│   ├── Dashboard/                 # Razor Pages web UI
│   └── Docs/                      # Server documentation
├── StorageWatchServer.Tests/      # Server tests
├── StorageWatchUI/                # WPF desktop application
│   ├── ViewModels/                # MVVM view models
│   ├── Views/                     # XAML views
│   ├── Services/                  # Data providers and service manager
│   ├── Communication/             # Named-pipe IPC client
│   ├── Styles/                    # Dark and light themes
│   └── Docs/                      # UI documentation
├── StorageWatchUI.Tests/          # UI tests
└── InstallerNSIS/                 # NSIS installer
    ├── StorageWatchInstaller.nsi  # Installer script
    ├── Payload/                   # Staged binaries (not in repo)
    └── Docs/                      # Installer documentation
```

---

## Alert State Machine

The `NotificationLoop` uses a three-state machine to minimize alert noise:

```
         ┌──────────┐
    ───▶ │  NORMAL  │ ◀────────────────────────────────────┐
         └────┬─────┘                                      │
              │ Free space < threshold                     │
              ▼                                            │
         ┌──────────┐                                      │
         │  ALERT   │ ────── Free space ≥ threshold ──────▶┘
         └────┬─────┘
              │ Drive not ready
              ▼
         ┌───────────┐
         │ NOT_READY │ ────── Drive ready ──────────────────▶ NORMAL
         └───────────┘
```

State is persisted to `%PROGRAMDATA%\StorageWatch\alert_state.json` across restarts.
