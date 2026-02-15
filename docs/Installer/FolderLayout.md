# StorageWatch Folder Layout

This document describes the complete folder structure created by the StorageWatch installer.

## 📁 Installation Directories

### Program Files (Binaries)

```
C:\Program Files\StorageWatch\
│
├── StorageWatch.exe                          # Windows Service executable
├── StorageWatch.exe.runtimeconfig.json       # .NET runtime configuration
├── Microsoft.Data.Sqlite.dll                 # SQLite ADO.NET provider
├── SQLitePCLRaw.core.dll                     # SQLite P/Invoke wrapper (core)
├── SQLitePCLRaw.provider.e_sqlite3.dll       # SQLite provider
├── SQLitePCLRaw.batteries_v2.dll             # SQLite batteries
├── e_sqlite3.dll                             # Native SQLite library
├── Microsoft.Extensions.Hosting.dll          # .NET hosting infrastructure
├── Microsoft.Extensions.Hosting.WindowsServices.dll  # Windows Service support
├── [Other .NET dependencies...]              # Additional framework libraries
│
├── UI\                                        # Desktop application folder
│   ├── StorageWatchUI.exe                    # WPF dashboard application
│   ├── StorageWatchUI.exe.runtimeconfig.json # .NET runtime configuration
│   ├── Microsoft.Data.Sqlite.dll             # SQLite for UI
│   ├── LiveChartsCore.dll                    # Charting library
│   ├── LiveChartsCore.SkiaSharpView.dll      # Chart rendering
│   ├── LiveChartsCore.SkiaSharpView.WPF.dll  # WPF chart integration
│   ├── SkiaSharp.dll                         # Graphics library
│   ├── appsettings.json                      # UI application settings
│   └── [Other UI dependencies...]            # Additional UI libraries
│
└── Plugins\                                   # Plugin folder (initially empty)
    └── [Plugin DLLs...]                      # Future: external alert sender plugins
```

**Permissions:**
- Read & Execute: All Users
- Modify: Administrators only
- Write: None (binaries are read-only)

### ProgramData (Configuration & Data)

```
C:\ProgramData\StorageWatch\
│
├── StorageWatchConfig.json                   # Main configuration file
│
├── Data\                                      # Database folder
│   ├── StorageWatch.db                       # Local SQLite database (agent mode)
│   ├── StorageWatch.db-wal                   # Write-ahead log (SQLite)
│   ├── StorageWatch.db-shm                   # Shared memory (SQLite)
│   └── StorageWatch_Central.db               # Central server database (server mode)
│
└── Logs\                                      # Log files folder
    ├── StorageWatch_20250101.log             # Daily log files
    ├── StorageWatch_20250102.log
    ├── StorageWatch_20250103.log
    └── [Older logs...]                       # Rotated log files
```

**Permissions:**
- Read & Write: Users, Administrators, SYSTEM
- Modify: Users (allows UI and service to write)
- Full Control: Administrators

**Note**: The installer grants Users group modify permissions to this folder to ensure both the service and the UI can read/write configuration and data.

### Start Menu

```
C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch\
│
├── StorageWatch Dashboard.lnk                # Launches StorageWatchUI.exe
└── Uninstall StorageWatch.lnk                # Launches Windows Installer uninstall
```

**Target Paths:**
- Dashboard: `C:\Program Files\StorageWatch\UI\StorageWatchUI.exe`
- Uninstall: `msiexec.exe /x {ProductCode}`

### Desktop (Optional)

```
C:\Users\[Username]\Desktop\
│
└── StorageWatch Dashboard.lnk                # Optional desktop shortcut
```

**Created When:**
- User selects "Create desktop shortcut" during installation
- Checkbox is checked by default

### Registry

```
HKEY_LOCAL_MACHINE\
│
├── SOFTWARE\StorageWatch Project\StorageWatch\
│   ├── InstallLocation    (REG_SZ)  "C:\Program Files\StorageWatch\"
│   ├── Version            (REG_SZ)  "1.0.0.0"
│   └── ConfigLocation     (REG_SZ)  "C:\ProgramData\StorageWatch\"
│
└── SYSTEM\CurrentControlSet\Services\StorageWatchService\
    ├── DisplayName        (REG_SZ)  "StorageWatch Service"
    ├── Description        (REG_SZ)  "Monitors disk space and provides alerts when thresholds are exceeded"
    ├── ImagePath          (REG_SZ)  "C:\Program Files\StorageWatch\StorageWatch.exe"
    ├── Start              (REG_DWORD) 2 (automatic)
    ├── Type               (REG_DWORD) 16 (own process)
    └── ObjectName         (REG_SZ)  "LocalSystem"

HKEY_CURRENT_USER\
│
└── SOFTWARE\StorageWatch\
    ├── StartMenu          (REG_SZ)  "" (key path marker)
    ├── Desktop            (REG_SZ)  "" (key path marker)
    └── Plugins            (REG_SZ)  "" (key path marker)
```

## 📊 Disk Space Requirements

### Minimum Installation
- Binaries (Program Files): ~50 MB
- Configuration (ProgramData): ~1 KB
- **Total Fresh Install**: ~50 MB

### After Operation
- SQLite Database: Varies (1-100 MB depending on retention policy)
- Log Files: Varies (10-50 MB depending on retention and verbosity)
- **Typical After 1 Month**: ~100-200 MB total

### Recommendation
- **Minimum Free Space**: 100 MB
- **Recommended Free Space**: 500 MB (for growth)

## 🔧 Configuration File Structure

### StorageWatchConfig.json

```json
{
  "StorageWatch": {
    "General": {
      "EnableStartupLogging": true
    },
    "Monitoring": {
      "ThresholdPercent": 10,
      "Drives": ["C:", "D:"]
    },
    "Database": {
      "ConnectionString": "Data Source=C:\\ProgramData\\StorageWatch\\Data\\StorageWatch.db;Version=3;"
    },
    "Alerting": {
      "EnableNotifications": true,
      "Smtp": {
        "Enabled": false,
        "Host": "smtp.example.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "",
        "Password": "",
        "FromAddress": "",
        "ToAddress": ""
      },
      "GroupMe": {
        "Enabled": false,
        "BotId": ""
      }
    },
    "CentralServer": {
      "Enabled": false,
      "Mode": "Agent",
      "ServerUrl": "",
      "ApiKey": "",
      "Port": 5000,
      "CentralConnectionString": "Data Source=C:\\ProgramData\\StorageWatch\\Data\\StorageWatch_Central.db;Version=3;",
      "ServerId": ""
    },
    "SqlReporting": {
      "Enabled": true,
      "RunMissedCollection": true,
      "RunOnlyOncePerDay": true,
      "CollectionTime": "02:00"
    }
  }
}
```

**File Characteristics:**
- **Encoding**: UTF-8 (no BOM)
- **Format**: JSON with indentation
- **Size**: ~1-2 KB
- **Permissions**: Users can read/write

## 📈 Database Schema

### StorageWatch.db (Agent Mode)

**Tables:**
- `disk_space_log` - Historical disk space measurements
- `alert_history` - Alert notification history
- `system_info` - Local system metadata

**Typical Size:**
- Empty: ~10 KB
- After 30 days (hourly checks): ~500 KB
- After 1 year (hourly checks): ~6 MB

### StorageWatch_Central.db (Server Mode)

**Tables:**
- `machines` - Registered machines
- `disk_space_log` - Aggregated disk space data from all machines
- `alert_history` - Centralized alert history
- `api_keys` - API authentication keys

**Typical Size:**
- 10 machines, 30 days: ~5 MB
- 100 machines, 30 days: ~50 MB
- 100 machines, 1 year: ~600 MB

## 🗑️ Uninstall Behavior

### Always Removed
- All files in `C:\Program Files\StorageWatch\` (binaries)
- All Start Menu shortcuts
- Desktop shortcut (if created)
- Windows Service registration
- Registry keys under `HKLM\SOFTWARE\StorageWatch Project`
- Registry keys under `HKCU\SOFTWARE\StorageWatch`

### Preserved by Default
- `C:\ProgramData\StorageWatch\StorageWatchConfig.json` (configuration)
- `C:\ProgramData\StorageWatch\Data\*.db` (databases)
- `C:\ProgramData\StorageWatch\Logs\*.log` (log files)

**Rationale**: Preserves user data and configuration for potential reinstallation.

### Manual Cleanup (if desired)
After uninstall, users can manually delete:
```
C:\ProgramData\StorageWatch\
```

To completely remove all traces of StorageWatch.

## 🔐 Security & Permissions Summary

| Path | Owner | Read | Write | Execute |
|------|-------|------|-------|---------|
| `Program Files\StorageWatch` | Administrators | All Users | Administrators | All Users |
| `ProgramData\StorageWatch` | Administrators | Users | Users | - |
| `ProgramData\StorageWatch\Data` | Administrators | Users | Users | - |
| `ProgramData\StorageWatch\Logs` | Administrators | Users | Users | - |
| Registry (HKLM) | SYSTEM | All Users | Administrators | - |

## 🚀 Runtime Paths

### Service Runtime
- **Executable**: `C:\Program Files\StorageWatch\StorageWatch.exe`
- **Working Directory**: `C:\Program Files\StorageWatch`
- **Config**: `C:\ProgramData\StorageWatch\StorageWatchConfig.json`
- **Database**: `C:\ProgramData\StorageWatch\Data\StorageWatch.db`
- **Logs**: `C:\ProgramData\StorageWatch\Logs\`

### UI Application Runtime
- **Executable**: `C:\Program Files\StorageWatch\UI\StorageWatchUI.exe`
- **Working Directory**: `C:\Program Files\StorageWatch\UI`
- **Config**: `C:\ProgramData\StorageWatch\StorageWatchConfig.json` (shared with service)
- **Database**: `C:\ProgramData\StorageWatch\Data\StorageWatch.db` (shared with service)

**Note**: Both the service and UI share the same configuration and database files, ensuring consistency.

## 📝 Path Resolution

The application uses the following logic to locate configuration and data:

1. Check environment variable: `STORAGEWATCH_CONFIG_PATH`
2. Check registry: `HKLM\SOFTWARE\StorageWatch Project\StorageWatch\ConfigLocation`
3. Default: `C:\ProgramData\StorageWatch\StorageWatchConfig.json`

This allows for custom installations or portable configurations if needed.
