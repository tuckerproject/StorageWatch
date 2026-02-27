# StorageWatch UI - User Guide

## Introduction

StorageWatchUI is a desktop application for monitoring disk space on Windows machines. It provides both local and centralized monitoring capabilities.

## Installation

1. Extract the StorageWatchUI files to a folder (e.g., `C:\Program Files\StorageWatch\`)
2. Ensure the StorageWatchAgent Windows Service is installed and running
3. Run `StorageWatchUI.exe` with Administrator privileges

**Note**: Administrator privileges are required for service management features.

## Main Window

The main window consists of two areas:

1. **Navigation Panel** (left): Buttons to switch between views
2. **Content Area** (right): The currently selected view

## Views

### 1. Dashboard

**Purpose**: Shows current disk status for the local machine.

**Features**:
- List of all monitored drives
- Color-coded status indicators:
  - 🟢 Green: OK (sufficient free space)
  - 🟠 Orange: Warning (approaching threshold)
  - 🔴 Red: Critical (below threshold)
- Total/free space in GB and percentage
- Progress bar showing used space
- Last update timestamp
- Manual refresh button
- Auto-refresh every 30 seconds

**What to do if no disks appear**:
1. Ensure StorageWatchAgent service is running (check Service Status view)
2. Verify the service has collected data (check service logs)
3. Confirm the database file exists at `%ProgramData%\StorageWatch\StorageWatch.db`

### 2. Trends

**Purpose**: View historical disk usage over time.

**Features**:
- Select a drive from the dropdown
- Choose time period (7, 14, 30, or 90 days)
- Interactive line chart showing free space percentage
- Data points correspond to collection times

**Chart Tips**:
- Hover over data points to see exact values
- Use the time period selector to zoom in/out
- Charts update when you change the selected drive

### 3. Central Server

**Purpose**: View all machines reporting to the central server.

**Features**:
- List of all reporting machines
- Online/offline status for each machine
- Last report timestamp
- Quick status summary for each disk
- Auto-refresh every 60 seconds

**If Central Server is not enabled**:
- A message will appear explaining that central server mode is disabled
- To enable it, go to Settings and configure the `CentralServer` section

**Machine Status Indicators**:
- 🟢 Online: Reported within the last 10 minutes
- 🔴 Offline: No report for more than 10 minutes

### 4. Settings

**Purpose**: View and manage configuration settings.

**Features**:
- Read-only display of the current configuration (JSON format)
- **Open Config in Notepad**: Opens the configuration file for editing
- **Test Alerts**: Send test alerts through configured senders (future feature)
- **Refresh**: Reload the configuration from disk

**Configuration File Location**:
- Primary: `%ProgramData%\StorageWatch\StorageWatchConfig.json`
- Fallback: Current directory

**Important**:
- After editing the config file, restart the StorageWatchAgent service for changes to take effect
- Invalid JSON will prevent the service from starting

### 5. Service Status

**Purpose**: Monitor and control the StorageWatchAgent Windows Service.

**Features**:
- Service installation status
- Current service state (Running, Stopped, etc.)
- Control buttons:
  - **Start Service**: Start the service if stopped
  - **Stop Service**: Stop the running service
  - **Restart Service**: Stop and start the service
- Last 20 log entries from the service log
- Auto-refresh every 10 seconds

**Service Control Requirements**:
- Must run StorageWatchUI as Administrator
- If buttons are grayed out, check that you have admin rights

**Log Viewer**:
- Shows the most recent 20 log entries
- Color coding for severity (Info, Warning, Error)
- Automatically refreshes with status updates
- Click "Refresh Logs" to manually update

## Auto-Refresh

Most views automatically refresh their data:
- **Dashboard**: Every 30 seconds
- **Trends**: Manual only (click Refresh)
- **Central**: Every 60 seconds
- **Settings**: Manual only
- **Service Status**: Every 10 seconds

## Troubleshooting

### "Configuration file not found"

**Cause**: The UI cannot locate `StorageWatchConfig.json`.

**Solution**:
1. Check `%ProgramData%\StorageWatch\` for the config file
2. Copy a sample config to this location
3. Restart the UI

### "No disk data available"

**Cause**: The service hasn't collected any data yet.

**Solution**:
1. Go to Service Status and verify the service is Running
2. Check the service logs for errors
3. Wait for the next scheduled collection (check `SqlReporting:CollectionTime` in config)
4. Verify the monitored drives are listed in the config

### "Cannot connect to central server"

**Cause**: The central server is not reachable or is disabled.

**Solution**:
1. Verify `CentralServer:Enabled` is `true` in config
2. Check `CentralServer:ServerUrl` is correct
3. Ensure the central server is running
4. Check network connectivity and firewall rules

### "Failed to start/stop service"

**Cause**: Insufficient permissions.

**Solution**:
1. Close the UI
2. Right-click `StorageWatchUI.exe` and choose "Run as administrator"
3. Try again

### Charts not displaying

**Cause**: No historical data in the database.

**Solution**:
1. Ensure the service has been running for at least 24 hours
2. Check that `SqlReporting:Enabled` is `true` in config
3. Verify data exists in the database (use SQLite browser tool)

## Keyboard Shortcuts

Currently, the UI uses mouse-driven navigation. Future versions may add keyboard shortcuts.

## Data Privacy

- All data is stored locally on your machine (SQLite database)
- Central server mode sends data to a server you control
- No data is sent to third parties
- Configuration files may contain sensitive information (passwords, API keys)

## Updating the UI

1. Close the UI application
2. Stop the StorageWatchAgent service (if updating both)
3. Replace the executable files
4. Start the service
5. Launch the UI

## Support

For issues, questions, or feature requests:
- GitHub Issues: [https://github.com/tuckerproject/DiskSpaceService/issues](https://github.com/tuckerproject/DiskSpaceService/issues)
- Documentation: See `Docs/` folder in the repository

## Version Information

Current Version: 1.0.0 (Phase 3, Item 11)
Target Framework: .NET 10
Minimum Windows Version: Windows 10
