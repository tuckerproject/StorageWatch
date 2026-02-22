# StorageWatchUI

A .NET 10 WPF desktop application providing a graphical interface for the StorageWatch monitoring platform.

---

## Overview

StorageWatchUI gives you a local, always-available window into your disk health. It displays real-time disk status, historical trends, and service control — without requiring a browser or a central server.

When a central StorageWatchServer is configured, the UI also shows a fleet-wide overview of all connected machines.

---

## Requirements

- Windows 10 or later
- .NET 10 Desktop Runtime
- StorageWatchService installed (recommended, but not required for read-only views)
- Administrator privileges (required for service start/stop/restart)

---

## Views

### Dashboard

Displays current disk usage for all monitored drives on the local machine.

- Drive letter, total/used/free space, percent free
- Color-coded status: **Green** (OK) · **Yellow** (Warning) · **Red** (Critical)
- Auto-refreshes every 30 seconds (configurable)

### Trends

Historical disk usage charts for the local machine.

- Pull data from the local SQLite database (`StorageWatch.db`)
- Select a drive and time period (7, 14, 30, or 90 days)
- Interactive line charts powered by LiveCharts2

### Central View

Aggregated view of all machines reporting to a central StorageWatchServer.

- Online/offline status per machine
- Last report timestamp
- Drive usage per machine
- Auto-refreshes every 60 seconds
- Requires `CentralServer.ServerUrl` to be configured in `StorageWatchConfig.json`

### Settings

Displays the current configuration.

- Read-only view of `StorageWatchConfig.json`
- **Open Config in Notepad** button for editing
- **Test Alerts** button to trigger a test notification

### Service Status

Monitors and controls the StorageWatchService Windows Service.

- Detects current service state (Running, Stopped, etc.)
- Start / Stop / Restart buttons (requires administrator)
- Displays the last 20 log entries from the service log
- Auto-refreshes every 10 seconds

---

## Quick Start

### From Installer

The NSIS installer places `StorageWatchUI.exe` in the Start Menu and installation directory. Launch it from the Start Menu or from:

```
%PROGRAMFILES%\StorageWatch\UI\StorageWatchUI.exe
```

### From Source

```powershell
dotnet run --project StorageWatchUI\StorageWatchUI.csproj
```

### Build and Publish

```powershell
dotnet publish StorageWatchUI\StorageWatchUI.csproj -c Release -f net10.0-windows -o publish\ui
```

---

## Configuration

The UI reads its settings from `appsettings.json` in the application directory:

```json
{
  "StorageWatchUI": {
    "RefreshIntervalSeconds": 30,
    "ChartDataPoints": 50,
    "Theme": "Dark"
  }
}
```

| Property | Default | Description |
|---|---|---|
| `RefreshIntervalSeconds` | `30` | How often the dashboard refreshes |
| `ChartDataPoints` | `50` | Maximum trend chart data points |
| `Theme` | `"Dark"` | `"Dark"` or `"Light"` |

---

## Themes

StorageWatchUI supports **Dark** and **Light** themes. Switch themes in `appsettings.json` and restart the application.

---

## Running as Administrator

Service control (start/stop/restart) requires administrator privileges. The application is configured with an administrator manifest. If you run without elevation, service control buttons will prompt for UAC.

---

## Documentation

- [User Guide](./Docs/UI/UserGuide.md) — Detailed per-view documentation
- [Architecture](./Docs/UI/Architecture.md) — MVVM architecture, data providers, IPC
- [Screenshots](./Docs/UI/Screenshots.md) — Visual overview of all views

---

## Dependencies

| Package | License |
|---|---|
| Microsoft.Data.Sqlite | MIT |
| Microsoft.Extensions.DependencyInjection | MIT |
| LiveChartsCore.SkiaSharpView.WPF | MIT |

---

## License

CC0 1.0 Universal (Public Domain)
