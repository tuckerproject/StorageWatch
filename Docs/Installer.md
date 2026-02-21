# StorageWatch Installer Documentation

## Overview

The StorageWatch Installer (`StorageWatchInstaller.exe`) is an NSIS-based Windows installer that supports two primary installation roles:

- **Agent Mode** — Local disk monitoring with optional central server reporting
- **Central Server Mode** — Multi-machine aggregation with web dashboard

---

## Installation Roles

### Agent Mode (Default)

**Purpose:** Monitors local disk usage on a single machine.

**What Gets Installed:**
- `StorageWatchService.exe` — Runs as a Windows Service
- `StorageWatchUI.exe` — Local GUI dashboard
- Local SQLite database for historical data
- Configuration and plugin folders

**Installation Path:**
```
$PROGRAMFILES64\StorageWatch\
├── Service\
├── UI\
└── (other support files)
```

**Windows Service:**
- Service Name: `StorageWatchService`
- Display Name: `StorageWatch Service`
- Startup Type: Automatic

**Configuration:**
- Service config: `%PROGRAMDATA%\StorageWatch\Config\StorageWatchConfig.json`
- Data directory: `%PROGRAMDATA%\StorageWatch\Data\`
- Logs directory: `%PROGRAMDATA%\StorageWatch\Logs\`

---

### Central Server Mode

**Purpose:** Aggregates data from multiple agent machines and hosts a web dashboard.

**What Gets Installed:**
- `StorageWatchServer.exe` — Runs as a Windows Service
- Razor Pages web application (dashboard)
- SQLite database for multi-machine data
- Server-specific configuration

**Installation Path:**
```
$PROGRAMFILES64\StorageWatch\
├── Service\         (optional, for agent mode agents)
├── UI\              (optional, local dashboard)
├── Server\
│   ├── appsettings.json
│   ├── StorageWatchServer.exe
│   ├── Data/
│   │   └── StorageWatchServer.db
│   ├── Logs/
│   └── (wwwroot, Razor Pages content)
└── (other support files)
```

**Windows Service:**
- Service Name: `StorageWatchServer`
- Display Name: `StorageWatch Central Server`
- Startup Type: Automatic

**Configuration:**
- Server config: `$INSTDIR\Server\appsettings.json` (generated during installation)
- Data directory: `$INSTDIR\Server\Data\` (configurable during setup)
- Logs directory: `$INSTDIR\Server\Logs\`

---

## Installation Process

### Step 1: Role Selection

The installer presents a dialog asking you to choose between:

1. **Agent (Local Monitoring)** — Default selection
   - Monitors local disks
   - Stores data locally
   - Optionally reports to a central server
   
2. **Central Server (Aggregation & Dashboard)**
   - Aggregates data from multiple agents
   - Hosts web dashboard
   - Requires network connectivity for agents to report

### Step 2: Server Configuration (Central Server Only)

If you select "Central Server," an additional configuration page appears:

- **Port:** Specify the port for the web dashboard (default: `5001`)
  - The dashboard will be accessible at `http://localhost:<port>`
  
- **Data Directory:** Path where the SQLite database will be stored (default: `$INSTDIR\Server\Data`)
  - Ensure sufficient disk space for multi-machine data
  - Must have write permissions for the service account

### Step 3: Components Selection

Standard NSIS component selection allows you to choose:

- **StorageWatch Service** — Required for Agent mode
- **StorageWatch Central Server** — Required for Central Server mode
- **StorageWatch UI** — Local dashboard (optional for servers)
- **Desktop Shortcut** — Optional shortcut on desktop
- **ProgramData** — Configuration and plugin folders

### Step 4: Installation

The installer copies files, creates directories, registers Windows Services, and configures permissions.

---

## Configuration

### Agent Mode Configuration

**File:** `%PROGRAMDATA%\StorageWatch\Config\StorageWatchConfig.json`

Default template included in the installer. Agents can optionally be configured to report to a central server via the `CentralServerUrl` setting.

### Central Server Configuration

**File:** `$INSTDIR\Server\appsettings.json`

Automatically generated during installation with the following structure:

```json
{
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "$INSTDIR/Server/Data/StorageWatchServer.db",
    "OnlineTimeoutMinutes": 10
  }
}
```

**Parameters:**
- `ListenUrl` — HTTP endpoint where the dashboard is hosted
- `DatabasePath` — Path to the SQLite database file
- `OnlineTimeoutMinutes` — Minutes before an agent is considered offline (default: 10)

---

## Shortcuts & Launch

### Agent Mode Shortcuts

- **Start Menu:** `StorageWatch Dashboard` → Launches `StorageWatchUI.exe`
- **Desktop:** Optional shortcut to the UI

### Central Server Mode Shortcuts

- **Start Menu → StorageWatch Central Dashboard** → Opens browser to dashboard URL
- **Start Menu → StorageWatch Server Logs** → Opens the server logs directory

---

## Directory Structure

### After Agent Installation

```
%PROGRAMDATA%\StorageWatch\
├── Config\
│   └── StorageWatchConfig.json
├── Plugins\
├── Logs\
└── Data\
    └── (SQLite database files)
```

### After Central Server Installation

```
$PROGRAMFILES64\StorageWatch\
├── Server\
│   ├── appsettings.json
│   ├── StorageWatchServer.exe
│   ├── Data\
│   │   └── StorageWatchServer.db
│   ├── Logs\
│   ├── wwwroot\
│   │   ├── css\
│   │   ├── js\
│   │   └── (other assets)
│   ├── Dashboard\
│   │   ├── Index.cshtml
│   │   ├── Machines\
│   │   ├── Alerts.cshtml
│   │   └── Settings.cshtml
│   └── (other binaries)
```

---

## Windows Service Management

### Starting/Stopping Services

**Agent Service:**
```powershell
# Start
Start-Service StorageWatchService

# Stop
Stop-Service StorageWatchService

# Check status
Get-Service StorageWatchService
```

**Server Service:**
```powershell
# Start
Start-Service StorageWatchServer

# Stop
Stop-Service StorageWatchServer

# Check status
Get-Service StorageWatchServer
```

### Manual Service Registration

If you need to manually register the service:

**Agent Service:**
```cmd
sc create StorageWatchService binPath= "C:\Program Files\StorageWatch\Service\StorageWatchService.exe" start= auto DisplayName= "StorageWatch Service"
```

**Server Service:**
```cmd
sc create StorageWatchServer binPath= "C:\Program Files\StorageWatch\Server\StorageWatchServer.exe" start= auto DisplayName= "StorageWatch Central Server"
```

---

## Permissions

The installer applies the following permissions:

**Service Accounts:**
- Services run under `LocalSystem` by default
- Can be configured post-installation if needed

**Folder Permissions:**
- `$INSTDIR\Server` — SYSTEM full control
- `$INSTDIR\Server\Data` — Users: Modify permission for database writes
- `$INSTDIR\Server\Logs` — Users: Modify permission for log writes

---

## Uninstallation

The uninstaller:

1. Stops and removes Windows Services (both Agent and Server)
2. Removes application files from `$INSTDIR`
3. Prompts to delete:
   - Configuration files
   - Logs
   - SQLite data
   - Plugins
4. Cleans up Start Menu shortcuts
5. Removes registry entries

**Note:** The server database (`StorageWatchServer.db`) is only deleted if you explicitly choose to remove data during uninstallation.

---

## Troubleshooting

### Service Won't Start

1. Verify the service is registered:
   ```powershell
   Get-Service StorageWatchService
   ```

2. Check Event Viewer for error details:
   - Open `Event Viewer` → `Windows Logs` → `Application`
   - Look for entries from `StorageWatchService` or `StorageWatchServer`

3. Ensure the executable path is correct in the service registration

### Dashboard Not Accessible

1. Verify the server service is running:
   ```powershell
   Get-Service StorageWatchServer
   ```

2. Check if the configured port is available:
   ```powershell
   netstat -ano | findstr :5001
   ```

3. Try accessing `http://localhost:5001` directly in your browser

4. Check server logs at `$INSTDIR\Server\Logs\`

### Permission Denied Errors

1. Run the uninstaller and reinstaller with **Administrator privileges**
2. Manually apply folder permissions if needed:
   ```cmd
   icacls "C:\Program Files\StorageWatch\Server\Data" /grant Users:(OI)(CI)M /T
   ```

---

## Updating StorageWatch

To update to a newer version:

1. Stop the service(s) using the Installer or Services panel
2. Run the new installer
3. Select the same role and configuration
4. The installer will overwrite application files while preserving configuration and data

---

## Support

For issues, questions, or contributions, visit the StorageWatch repository or refer to the main project documentation.
