# Central Web Dashboard — Implementation Documentation

**Phase 4, Step 14: Central Web Dashboard**

This document provides complete reference material for the StorageWatchServer Central Web Dashboard, including API contracts, database schema, dashboard pages, and integration guidelines.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Database Schema](#database-schema)
4. [REST API Reference](#rest-api-reference)
5. [Dashboard Pages](#dashboard-pages)
6. [Agent Reporting Payload Format](#agent-reporting-payload-format)
7. [Online/Offline Detection](#onlineoffline-detection)
8. [Server Mode Behavior](#server-mode-behavior)
9. [Configuration Reference](#configuration-reference)
10. [Error Handling](#error-handling)
11. [Testing](#testing)
12. [Deployment](#deployment)

---

## Overview

The **StorageWatchServer** central web dashboard is a multi-machine storage monitoring platform that:

- **Aggregates** data from multiple agent machines
- **Displays** real-time and historical storage metrics
- **Detects** online/offline status with configurable timeout
- **Manages** alerts and system settings
- **Separates** data by machine for multi-tenant scenarios

### Key Features

✅ Multi-machine aggregation  
✅ RESTful API for agent reporting  
✅ Real-time online/offline detection  
✅ Historical trend charts (last 7 days, 24h, 1d)  
✅ Alert management and display  
✅ Server configuration visibility  
✅ Structured logging for all operations  
✅ Graceful error handling  

---

## Architecture

### Technology Stack

- **Framework**: ASP.NET Core 10 (Kestrel)
- **Database**: SQLite (local, file-based)
- **UI**: Razor Pages
- **Charts**: Chart.js 4.4.1
- **Logging**: ILogger (Microsoft.Extensions.Logging)

### Component Structure

```
StorageWatchServer/
├── Program.cs                    # Startup configuration
├── Server/
│   ├── Api/
│   │   ├── ApiEndpoints.cs       # REST API routes & handlers
│   │   ├── AgentReportRequest.cs # API payload types
│   │   └── ApiResponse.cs        # Standard response wrapper
│   ├── Data/
│   │   ├── ServerRepository.cs   # Database operations
│   │   └── ServerSchema.cs       # Schema initialization
│   ├── Models/
│   │   ├── MachineSummary.cs     # List view model
│   │   ├── MachineDetails.cs     # Detail view model
│   │   ├── MachineDriveStatus.cs # Current drive status
│   │   ├── DiskHistoryPoint.cs   # Historical data point
│   │   ├── AlertRecord.cs        # Alert metadata
│   │   └── SettingRecord.cs      # Configuration setting
│   └── Services/
│       ├── MachineStatusService.cs  # Online/offline detection
│       └── ServerOptions.cs         # Configuration binding
├── Dashboard/
│   ├── Index.cshtml(.cs)         # Dashboard home (machine list)
│   ├── Alerts.cshtml(.cs)        # Alert management
│   ├── Settings.cshtml(.cs)      # Server settings (read-only)
│   ├── Machines/
│   │   └── Details.cshtml(.cs)   # Machine details + trends
│   └── Shared/
│       └── _Layout.cshtml        # Master layout
└── wwwroot/
    └── css/
        └── site.css              # Dashboard styling
```

---

## Database Schema

### Tables

#### **Machines**
Stores information about connected agent machines.

```sql
CREATE TABLE Machines (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineName TEXT NOT NULL UNIQUE,
    LastSeenUtc DATETIME NOT NULL,
    CreatedUtc DATETIME NOT NULL
);
```

| Column | Type | Purpose |
|--------|------|---------|
| `Id` | INTEGER | Unique machine identifier |
| `MachineName` | TEXT | Agent hostname or custom name |
| `LastSeenUtc` | DATETIME | Last report timestamp (UTC) |
| `CreatedUtc` | DATETIME | First registration timestamp (UTC) |

---

#### **MachineDrives**
Current drive status for each machine (latest snapshot).

```sql
CREATE TABLE MachineDrives (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineId INTEGER NOT NULL,
    DriveLetter TEXT NOT NULL,
    TotalSpaceGb REAL NOT NULL,
    UsedSpaceGb REAL NOT NULL,
    FreeSpaceGb REAL NOT NULL,
    PercentFree REAL NOT NULL,
    LastSeenUtc DATETIME NOT NULL,
    UNIQUE(MachineId, DriveLetter)
);
```

| Column | Type | Purpose |
|--------|------|---------|
| `MachineId` | INTEGER | Foreign key to `Machines` |
| `DriveLetter` | TEXT | Drive identifier (e.g., "C:") |
| `TotalSpaceGb` | REAL | Total capacity in GB |
| `UsedSpaceGb` | REAL | Used space in GB |
| `FreeSpaceGb` | REAL | Free space in GB |
| `PercentFree` | REAL | Percentage free (0-100) |
| `LastSeenUtc` | DATETIME | Last update timestamp (UTC) |

---

#### **DiskHistory**
Historical disk usage records (time-series data).

```sql
CREATE TABLE DiskHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineId INTEGER NOT NULL,
    DriveLetter TEXT NOT NULL,
    TotalSpaceGb REAL NOT NULL,
    UsedSpaceGb REAL NOT NULL,
    FreeSpaceGb REAL NOT NULL,
    PercentFree REAL NOT NULL,
    CollectionTimeUtc DATETIME NOT NULL
);

CREATE INDEX idx_DiskHistory_Machine_Drive_Time
ON DiskHistory(MachineId, DriveLetter, CollectionTimeUtc);
```

| Column | Type | Purpose |
|--------|------|---------|
| `MachineId` | INTEGER | Foreign key to `Machines` |
| `DriveLetter` | TEXT | Drive identifier |
| `TotalSpaceGb...PercentFree` | REAL | Same as `MachineDrives` |
| `CollectionTimeUtc` | DATETIME | Timestamp for this measurement (UTC) |

---

#### **Alerts**
Alert events and their resolution status.

```sql
CREATE TABLE Alerts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MachineId INTEGER NOT NULL,
    Severity TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    ResolvedUtc DATETIME NULL,
    IsActive INTEGER NOT NULL
);
```

| Column | Type | Purpose |
|--------|------|---------|
| `MachineId` | INTEGER | Foreign key to `Machines` |
| `Severity` | TEXT | Level: "Info", "Warning", "Critical" |
| `Message` | TEXT | Alert description |
| `CreatedUtc` | DATETIME | When alert was triggered (UTC) |
| `ResolvedUtc` | DATETIME NULL | When alert was resolved (UTC), if applicable |
| `IsActive` | INTEGER | Boolean (1=active, 0=resolved) |

---

#### **Settings**
Server configuration values (key-value store).

```sql
CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    Description TEXT NOT NULL
);
```

| Column | Type | Purpose |
|--------|------|---------|
| `Key` | TEXT | Setting identifier (e.g., "OnlineTimeoutMinutes") |
| `Value` | TEXT | Configuration value |
| `Description` | TEXT | Human-readable explanation |

**Default Settings:**

| Key | Default Value | Description |
|-----|---------------|-------------|
| `OnlineTimeoutMinutes` | `10` | Minutes before a machine is considered offline |
| `ListenUrl` | `http://localhost:5001` | Kestrel listen address |
| `DatabasePath` | `Data/StorageWatchServer.db` | SQLite database file location |

---

## REST API Reference

### Base URL
```
http://localhost:5001/api
```

All responses use the standard `ApiResponse` wrapper (for errors) or direct data objects (for success).

---

### POST /api/agent/report

**Purpose**: Accept disk usage reports from agent machines.

**Request Headers**
```
Content-Type: application/json
```

**Request Body**
```json
{
  "machineName": "LAPTOP-ABC123",
  "collectionTimeUtc": "2024-01-15T14:30:00Z",
  "drives": [
    {
      "driveLetter": "C:",
      "totalSpaceGb": 500.0,
      "usedSpaceGb": 350.0,
      "freeSpaceGb": 150.0,
      "percentFree": 30.0,
      "collectionTimeUtc": "2024-01-15T14:30:00Z"
    },
    {
      "driveLetter": "D:",
      "totalSpaceGb": 1000.0,
      "usedSpaceGb": 200.0,
      "freeSpaceGb": 800.0,
      "percentFree": 80.0,
      "collectionTimeUtc": "2024-01-15T14:30:00Z"
    }
  ]
}
```

**Response (200 OK)**
```json
{
  "success": true,
  "message": "Report received.",
  "data": null
}
```

**Response (400 Bad Request)**
```json
{
  "success": false,
  "message": "MachineName and at least one drive are required.",
  "data": null
}
```

**Status Codes**
- `200 OK` — Report accepted and processed
- `400 Bad Request` — Validation failed (missing MachineName or no drives)
- `500 Internal Server Error` — Database or processing error

**Behavior**
1. Validates `machineName` is not empty and `drives.length > 0`
2. Upserts machine record with current `LastSeenUtc`
3. For each drive:
   - Upserts current status in `MachineDrives`
   - Inserts history record in `DiskHistory`
4. Returns success response

---

### GET /api/machines

**Purpose**: Retrieve a summary list of all connected machines.

**Query Parameters**: None

**Response (200 OK)**
```json
[
  {
    "id": 1,
    "machineName": "LAPTOP-ABC123",
    "lastSeenUtc": "2024-01-15T14:30:00Z",
    "isOnline": true,
    "drives": [
      {
        "driveLetter": "C:",
        "totalSpaceGb": 500.0,
        "usedSpaceGb": 350.0,
        "freeSpaceGb": 150.0,
        "percentFree": 30.0,
        "lastSeenUtc": "2024-01-15T14:30:00Z"
      }
    ]
  }
]
```

**Status Codes**
- `200 OK` — List returned successfully (may be empty)
- `500 Internal Server Error` — Database error

---

### GET /api/machines/{id}

**Purpose**: Retrieve full details for a specific machine.

**Path Parameters**
- `id` (int) — Machine ID

**Response (200 OK)**
```json
{
  "id": 1,
  "machineName": "LAPTOP-ABC123",
  "lastSeenUtc": "2024-01-15T14:30:00Z",
  "createdUtc": "2024-01-10T08:00:00Z",
  "isOnline": true,
  "drives": [
    {
      "driveLetter": "C:",
      "totalSpaceGb": 500.0,
      "usedSpaceGb": 350.0,
      "freeSpaceGb": 150.0,
      "percentFree": 30.0,
      "lastSeenUtc": "2024-01-15T14:30:00Z"
    }
  ]
}
```

**Response (404 Not Found)**
```json
null
```

**Status Codes**
- `200 OK` — Machine found and returned
- `404 Not Found` — Machine ID does not exist
- `500 Internal Server Error` — Database error

---

### GET /api/machines/{id}/history

**Purpose**: Retrieve historical disk usage for a specific machine and drive.

**Path Parameters**
- `id` (int) — Machine ID

**Query Parameters**
- `drive` (string, **required**) — Drive letter (e.g., "C:")
- `range` (string, optional) — Time range filter
  - `1d` — Last 1 day (default: 7 days)
  - `7d` — Last 7 days
  - `30d` — Last 30 days
  - `24h` — Last 24 hours
  - Any number followed by `d` or `h`

**Example Requests**
```
GET /api/machines/1/history?drive=C:
GET /api/machines/1/history?drive=C:&range=7d
GET /api/machines/1/history?drive=C:&range=24h
```

**Response (200 OK)**
```json
[
  {
    "collectionTimeUtc": "2024-01-14T14:30:00Z",
    "totalSpaceGb": 500.0,
    "usedSpaceGb": 340.0,
    "freeSpaceGb": 160.0,
    "percentFree": 32.0
  },
  {
    "collectionTimeUtc": "2024-01-15T14:30:00Z",
    "totalSpaceGb": 500.0,
    "usedSpaceGb": 350.0,
    "freeSpaceGb": 150.0,
    "percentFree": 30.0
  }
]
```

**Response (400 Bad Request)**
```json
{
  "success": false,
  "message": "Drive letter is required.",
  "data": null
}
```

**Status Codes**
- `200 OK` — History retrieved (may be empty)
- `400 Bad Request` — Missing `drive` parameter
- `500 Internal Server Error` — Database error

---

### GET /api/alerts

**Purpose**: Retrieve all active and historical alerts.

**Query Parameters**: None

**Response (200 OK)**
```json
[
  {
    "id": 1,
    "machineId": 1,
    "machineName": "LAPTOP-ABC123",
    "severity": "Critical",
    "message": "Drive C: is running low on free space (10% remaining)",
    "createdUtc": "2024-01-15T14:00:00Z",
    "resolvedUtc": null,
    "isActive": true
  },
  {
    "id": 2,
    "machineId": 1,
    "machineName": "LAPTOP-ABC123",
    "severity": "Warning",
    "message": "Machine offline for 15 minutes",
    "createdUtc": "2024-01-15T13:00:00Z",
    "resolvedUtc": "2024-01-15T13:15:00Z",
    "isActive": false
  }
]
```

**Status Codes**
- `200 OK` — Alerts retrieved (may be empty)
- `500 Internal Server Error` — Database error

**Ordering**: Active alerts first, then by creation time (newest first)

---

### GET /api/settings

**Purpose**: Retrieve server configuration settings.

**Query Parameters**: None

**Response (200 OK)**
```json
[
  {
    "key": "OnlineTimeoutMinutes",
    "value": "10",
    "description": "Minutes before a machine is considered offline."
  },
  {
    "key": "ListenUrl",
    "value": "http://localhost:5001",
    "description": "The URL Kestrel listens on for dashboard traffic."
  },
  {
    "key": "DatabasePath",
    "value": "Data/StorageWatchServer.db",
    "description": "The SQLite database location for the central dashboard."
  }
]
```

**Status Codes**
- `200 OK` — Settings retrieved
- `500 Internal Server Error` — Database error

---

## Dashboard Pages

### Page: Index (Dashboard Home)

**Route**: `/` or `/index`

**File**: `Dashboard/Index.cshtml`

**Purpose**: Display a quick overview of all connected machines.

**Features**
- Machine list with online/offline status
- Real-time free space percentage for each drive
- Quick-link to machine details
- Empty state message when no machines have reported

**Data Source**
- API endpoint: `GET /api/machines`
- Status service: Online/offline detection

**Rendered With**
- MachineSummaryView objects
- Bootstrap table styling
- Status badge colors (green=online, red=offline)

**Example UI Layout**
```
[StorageWatch Server Header]
        Dashboard | Alerts | Settings

Central Dashboard
================
Machines
┌───────────────┬────────┬──────────────────┬─────────────┬──────────────┐
│ Machine       │ Status │ Last Seen (UTC)  │ Drives      │              │
├───────────────┼────────┼──────────────────┼─────────────┼──────────────┤
│ LAPTOP-ABC123 │ Online │ 2024-01-15 14:30 │ C: 30%      │ [Details]    │
│               │        │                  │ D: 80%      │              │
├───────────────┼────────┼──────────────────┼─────────────┼──────────────┤
│ SERVER-XYZ    │ Offline│ 2024-01-14 18:45 │ C: 45%      │ [Details]    │
└───────────────┴────────┴──────────────────┴─────────────┴──────────────┘
```

---

### Page: Machines / Details

**Route**: `/machines/{id}`

**File**: `Dashboard/Machines/Details.cshtml`

**Purpose**: Show detailed information and trends for a specific machine.

**Features**
- Machine metadata (name, status, timestamps)
- Current drive list with full metrics
- Historical charts for last 7 days
- Chart.js integration for smooth visualization
- Error handling with user-friendly messages

**Data Sources**
- API endpoint: `GET /api/machines/{id}`
- API endpoint: `GET /api/machines/{id}/history?drive={letter}&range=7d`
- Status service: Online/offline detection

**Chart Specifications**
- **Type**: Line chart
- **X-axis**: Time (formatted as "MM-dd HH:mm")
- **Y-axis**: Percent Free (0-100%)
- **Color**: Blue with light blue background
- **Responsive**: Grid layout with auto-fit columns

**Example UI Layout**
```
LAPTOP-ABC123
==============
Status: Online
Last Seen: 2024-01-15 14:30 UTC
Created: 2024-01-10 08:00 UTC

Drives
┌──────┬──────────┬────────┬─────────┬──────────┬──────────────┐
│ Drive│ Total GB │ Used GB│ Free GB │ Free %   │ Last Seen    │
├──────┼──────────┼────────┼─────────┼──────────┼──────────────┤
│ C:   │ 500.0    │ 350.0  │ 150.0   │ 30.0%    │ 2024-01-15   │
│ D:   │ 1000.0   │ 200.0  │ 800.0   │ 80.0%    │ 2024-01-15   │
└──────┴──────────┴────────┴─────────┴──────────┴──────────────┘

Disk History (Last 7 Days)
[Chart C:] [Chart D:]
```

---

### Page: Alerts

**Route**: `/alerts`

**File**: `Dashboard/Alerts.cshtml`

**Purpose**: View and manage all alerts across all machines.

**Features**
- Complete alert list (active and resolved)
- Machine association for each alert
- Severity level display
- Status indicators (Active/Resolved)
- Empty state message

**Data Source**
- API endpoint: `GET /api/alerts`

**Alert Severity Levels**
- `Info` — Informational messages
- `Warning` — Non-critical issues
- `Critical` — Urgent problems requiring attention

**Example UI Layout**
```
Alerts
======
┌──────────────┬──────────┬──────────────────────────┬──────────┬──────────┐
│ Machine      │ Severity │ Message                  │ Created  │ Status   │
├──────────────┼──────────┼──────────────────────────┼──────────┼──────────┤
│ LAPTOP-ABC123│ Critical │ Drive C: low (10% free)  │ 2024-..  │ Active   │
├──────────────┼──────────┼──────────────────────────┼──────────┼──────────┤
│ SERVER-XYZ   │ Warning  │ Machine offline > 15min  │ 2024-..  │ Resolved │
└──────────────┴──────────┴──────────────────────────┴──────────┴──────────┘
```

---

### Page: Settings

**Route**: `/settings`

**File**: `Dashboard/Settings.cshtml`

**Purpose**: Display server configuration (read-only view).

**Features**
- Display all configuration keys and values
- Show descriptions for each setting
- Read-only display (no editing)
- Code-formatted values for clarity

**Data Source**
- API endpoint: `GET /api/settings`

**Example UI Layout**
```
Settings
========
┌────────────────────┬─────────────────────┬───────────────────────┐
│ Key                │ Value               │ Description           │
├────────────────────┼─────────────────────┼───────────────────────┤
│ OnlineTimeoutMin   │ 10                  │ Minutes before...     │
│ ListenUrl          │ http://localhost... │ The URL Kestrel...    │
│ DatabasePath       │ Data/StorageWatch.. │ The SQLite database.. │
└────────────────────┴─────────────────────┴───────────────────────┘
```

---

## Agent Reporting Payload Format

### JSON Schema

Agents use the `AgentReportRequest` class to send data to the server.

**C# Definition**
```csharp
public class AgentReportRequest
{
    public string MachineName { get; set; } = string.Empty;
    public DateTime CollectionTimeUtc { get; set; }
    public List<AgentDriveReport> Drives { get; set; } = new();
}

public class AgentDriveReport
{
    public string DriveLetter { get; set; } = string.Empty;
    public double TotalSpaceGb { get; set; }
    public double UsedSpaceGb { get; set; }
    public double FreeSpaceGb { get; set; }
    public double PercentFree { get; set; }
    public DateTime CollectionTimeUtc { get; set; }
}
```

**JSON Example**
```json
{
  "machineName": "LAPTOP-ABC123",
  "collectionTimeUtc": "2024-01-15T14:30:00Z",
  "drives": [
    {
      "driveLetter": "C:",
      "totalSpaceGb": 500.0,
      "usedSpaceGb": 350.0,
      "freeSpaceGb": 150.0,
      "percentFree": 30.0,
      "collectionTimeUtc": "2024-01-15T14:30:00Z"
    },
    {
      "driveLetter": "D:",
      "totalSpaceGb": 1000.0,
      "usedSpaceGb": 200.0,
      "freeSpaceGb": 800.0,
      "percentFree": 80.0,
      "collectionTimeUtc": "2024-01-15T14:30:00Z"
    }
  ]
}
```

### Field Specifications

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `machineName` | string | ✅ Yes | Must be non-empty; identifies the agent |
| `collectionTimeUtc` | ISO 8601 | ❌ No | Defaults to server time if omitted |
| `drives[].driveLetter` | string | ✅ Yes | Format: "C:", "D:", etc. |
| `drives[].totalSpaceGb` | number | ✅ Yes | Must be > 0 |
| `drives[].usedSpaceGb` | number | ✅ Yes | Must be ≤ totalSpaceGb |
| `drives[].freeSpaceGb` | number | ✅ Yes | Should equal totalSpaceGb - usedSpaceGb |
| `drives[].percentFree` | number | ✅ Yes | 0-100% |
| `drives[].collectionTimeUtc` | ISO 8601 | ❌ No | Defaults to report timestamp |

### Validation Rules

1. **MachineName** must not be null, empty, or whitespace
2. **Drives** array must contain at least one drive
3. **DriveLetter** must follow Windows convention (letter + colon)
4. **Space values** must be non-negative
5. **PercentFree** should be between 0 and 100

### C# Example (Agent-Side)

```csharp
var report = new AgentReportRequest
{
    MachineName = Environment.MachineName,
    CollectionTimeUtc = DateTime.UtcNow,
    Drives = drives.Select(drive => new AgentDriveReport
    {
        DriveLetter = drive.Name,
        TotalSpaceGb = drive.TotalSize / (1024d * 1024d * 1024d),
        UsedSpaceGb = (drive.TotalSize - drive.AvailableFreeSpace) / (1024d * 1024d * 1024d),
        FreeSpaceGb = drive.AvailableFreeSpace / (1024d * 1024d * 1024d),
        PercentFree = (drive.AvailableFreeSpace * 100.0) / drive.TotalSize,
        CollectionTimeUtc = DateTime.UtcNow
    }).ToList()
};

var json = JsonSerializer.Serialize(report);
using var client = new HttpClient();
var response = await client.PostAsJsonAsync("http://server:5001/api/agent/report", report);
```

---

## Online/Offline Detection

### How It Works

The `MachineStatusService` determines online status based on `LastSeenUtc`:

```csharp
public bool IsOnline(DateTime lastSeenUtc)
{
    return lastSeenUtc >= GetOnlineThresholdUtc();
}

public DateTime GetOnlineThresholdUtc()
{
    return DateTime.UtcNow.AddMinutes(-_options.OnlineTimeoutMinutes);
}
```

### Configuration

**appsettings.json**
```json
{
  "Server": {
    "OnlineTimeoutMinutes": 10
  }
}
```

**Default**: 10 minutes

### Examples

- If `OnlineTimeoutMinutes = 10`
- Current time: 2024-01-15 14:30:00 UTC
- Threshold: 2024-01-15 14:20:00 UTC (10 minutes ago)

| LastSeenUtc | Status |
|-------------|--------|
| 14:29:00 | **Online** ✅ (within 10 min) |
| 14:20:00 | **Offline** ❌ (exactly at threshold) |
| 14:15:00 | **Offline** ❌ (beyond threshold) |

### Dashboard Integration

- **Index page**: Displays status badge (green/red) for each machine
- **Details page**: Shows status at top of page
- **API response**: Includes `isOnline` boolean in machine objects

---

## Server Mode Behavior

### Startup Logging

When StorageWatchServer starts in server mode, it logs:

```
StorageWatch Server starting in server mode...
Server listening on: http://localhost:5001
Database path: Data/StorageWatchServer.db
Online timeout: 10 minutes
Database initialized successfully
StorageWatch Server ready to accept connections
```

### Server-Only Features

The following are **only enabled** when running StorageWatchServer:

✅ REST API (`/api/*`)  
✅ Web Dashboard (`/`, `/machines/*`, `/alerts`, `/settings`)  
✅ SQLite multi-machine database  
✅ Agent aggregation  
✅ Alert management  

### Agent vs. Server

| Feature | StorageWatchAgent (Agent) | StorageWatchServer (Server) |
|---------|-------|--------|
| Local disk monitoring | ✅ Yes | ❌ No |
| Local SQLite database | ✅ Yes | ✅ Yes (multi-machine) |
| REST API server | ❌ No | ✅ Yes |
| Web dashboard | ❌ No | ✅ Yes |
| Receive agent reports | ❌ No | ✅ Yes |
| Send reports to server | ✅ Optional | ❌ N/A |

---

## Configuration Reference

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "Data/StorageWatchServer.db",
    "OnlineTimeoutMinutes": 10
  }
}
```

### ServerOptions.cs

```csharp
public class ServerOptions
{
    public string ListenUrl { get; set; } = "http://localhost:5001";
    public string DatabasePath { get; set; } = "Data/StorageWatchServer.db";
    public int OnlineTimeoutMinutes { get; set; } = 10;
}
```

### Environment Variables

Override appsettings via environment:

```bash
# Listen on a different URL
set Server__ListenUrl=http://0.0.0.0:8080

# Use custom database location
set Server__DatabasePath=C:\data\agents.db

# Change timeout to 5 minutes
set Server__OnlineTimeoutMinutes=5
```

### Command-Line Arguments

Pass as JSON file:

```bash
dotnet StorageWatchServer.dll --configuration custom.json
```

---

## Error Handling

### API Error Responses

All API endpoints follow a consistent error pattern:

**Structure**
```json
{
  "success": false,
  "message": "Human-readable error description",
  "data": null
}
```

**Examples**

**Validation Error (400 Bad Request)**
```json
{
  "success": false,
  "message": "MachineName and at least one drive are required.",
  "data": null
}
```

**Not Found (404 Not Found)**
```json
null
```

**Internal Error (500 Internal Server Error)**
```
[No body, HTTP status only]
```

### Logging

All errors are logged to the application logs:

```
[ERROR] Error processing agent report from LAPTOP-ABC123
System.InvalidOperationException: Database connection failed
  at StorageWatchServer.Server.Data.ServerRepository.UpsertMachineAsync(...)
```

### Dashboard Error Handling

- **Razor pages** catch exceptions and display user-friendly messages
- **Error cards** styled with red background and border
- **Fallback UI** shows empty states when data unavailable

---

## Testing

### Unit Tests (StorageWatchServer.Tests)

#### Services

**MachineStatusServiceTests**
- IsOnline with recent timestamp → True
- IsOnline with old timestamp → False
- IsOnline at threshold → False
- IsOnline just before threshold → True

#### Repository

**ServerRepositoryTests**
- Upsert new machine
- Upsert duplicate machine (updates)
- Get machines (returns all)
- Get machine by ID
- Upsert drives
- Insert history
- Query history by date range
- Get alerts
- Get settings
- Multi-machine separation

### Integration Tests

**ApiEndpointsIntegrationTests** (using WebApplicationFactory)
- POST `/api/agent/report` with valid payload
- POST `/api/agent/report` with missing data
- GET `/api/machines`
- GET `/api/machines/{id}`
- GET `/api/machines/{id}/history` with filters
- GET `/api/alerts`
- GET `/api/settings`

**DashboardPagesTests**
- Index page loads
- Alerts page loads
- Settings page loads
- Machine details loads
- Navigation links present

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter FullyQualifiedName~MachineStatusServiceTests

# Run with coverage
dotnet test /p:CollectCoverage=true
```

---

## Deployment

### System Requirements

- **OS**: Windows 10+, Linux, macOS
- **.NET Runtime**: .NET 10 (or .NET 10 hosting bundle on Windows)
- **Database**: SQLite (included)
- **Ports**: Configurable (default: 5001)
- **Disk**: Minimal; grows with historical data

### Installation Steps

1. **Extract/publish** StorageWatchServer binaries
2. **Create** `appsettings.json` with desired settings
3. **Create** data directory: `mkdir Data`
4. **Run** `dotnet StorageWatchServer.dll` (or `.exe` on Windows)
5. **Navigate** to `http://localhost:5001` in browser

### Windows Service (Optional)

To run as Windows service:

```powershell
# Install
sc create StorageWatchServer binPath= "C:\Path\To\StorageWatchServer.exe"

# Start
net start StorageWatchServer

# Stop
net stop StorageWatchServer
```

### Docker (Optional)

Example Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY StorageWatchServer .
EXPOSE 5001
ENTRYPOINT ["dotnet", "StorageWatchServer.dll"]
```

Build & run:
```bash
docker build -t storagewatch-server .
docker run -p 5001:5001 -v /data:/app/Data storagewatch-server
```

---

## FAQ

**Q: Can I change the dashboard port?**  
A: Yes, modify `Server.ListenUrl` in appsettings.json or set environment variable `Server__ListenUrl`.

**Q: Where is the database stored?**  
A: By default in `Data/StorageWatchServer.db`. Change via `Server.DatabasePath`.

**Q: How long is data retained?**  
A: No automatic cleanup currently. All historical data is kept indefinitely. (Future feature: retention policies)

**Q: Can multiple servers aggregate data?**  
A: Not currently. Each server has its own independent database. Federation is a future roadmap item.

**Q: How do I clear all data and start fresh?**  
A: Stop the server, delete `Data/StorageWatchServer.db`, and restart.

**Q: Is the dashboard password-protected?**  
A: Not in this version. Authentication is a future roadmap item (Phase 5).

---

## Related Documentation

- [StorageWatch CopilotMasterPrompt.md](./CopilotMasterPrompt.md) — Full roadmap
- [Server Communication Architecture](./ServiceCommunication/Architecture.md) — Agent/server communication
- [Configuration Redesign](./ConfigurationRedesignDiff.md) — Configuration system details

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-15  
**Phase**: 4 (Advanced Features), Step 14
