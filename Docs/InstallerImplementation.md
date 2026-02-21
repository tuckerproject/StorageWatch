# Step 14.5 Implementation Summary: Central Server Installer Support

## Overview

This implementation adds Central Server installation support to the StorageWatch NSIS installer, completing **Step 14.5** of the roadmap. The installer now supports two distinct installation roles:

1. **Agent Mode** — Local disk monitoring (original functionality)
2. **Central Server Mode** — Multi-machine aggregation with web dashboard

---

## Changes Made

### 1. Updated NSIS Installer (`InstallerNSIS\StorageWatchInstaller.nsi`)

#### Key Enhancements:

**A. Role Selection Page**
- New custom page (`RoleSelectionPage`) presented after Components page
- Two radio button options:
  - Agent (Local Monitoring)
  - Central Server (Aggregation & Dashboard)
- Default selection: Agent (backward compatible)
- Stores selected role in `$SelectedRole` variable and registry

**B. Server Configuration Page**
- New custom page (`ServerConfigPage`) shown only when "Central Server" is selected
- User inputs:
  - **Port:** HTTP listening port (default: 5001)
  - **Data Directory:** SQLite database location (default: `$INSTDIR\Server\Data`)
- Page includes helpful description of server capabilities

**C. New Section: StorageWatch Central Server**
- Installs server executable and supporting files to `$INSTDIR\Server\`
- Creates required directories: `Data/` and `Logs/`
- Generates server configuration file dynamically
- Registers Windows Service with appropriate name/display name

**D. Enhanced Service Management**
- Separate functions for server service:
  - `InstallServerService()` — Registers StorageWatchServer service
  - `StartServerService()` — Starts server service
  - `StopServerIfRunning()` — Gracefully stops server service
  - `StopAndRemoveServerService()` — Removes service during uninstall
- Updated `StartService()` in PostInstall section to route based on role

**E. Dynamic Configuration Generation**
- `GenerateServerConfig()` function creates `appsettings.json` on-the-fly
- Injects user-selected port and data directory
- Ensures correct file paths with proper escaping

**F. Start Menu Shortcuts (Server Mode)**
- Dashboard shortcut: Opens default browser to `http://localhost:<port>`
- Server Logs shortcut: Opens logs directory in Explorer

**G. Permission Management**
- `ApplyFolderPermissions()` extended to handle server directories
- Sets appropriate NTFS permissions for `$INSTDIR\Server\Data` and `Logs` for user modifications

**H. Uninstall Support**
- New function `PromptDeleteServerData()` for database retention options
- Removes server shortcuts and files
- Allows preservation of `StorageWatchServer.db` for data retention

**I. Initialization Check**
- `.onInit` updated to detect and stop both Agent and Server services if already installed

---

### 2. Installer Documentation (`Docs\Installer.md`)

Comprehensive documentation covering:

- **Installation Roles** — Detailed explanation of Agent vs. Server modes
- **Installation Process** — Step-by-step walkthrough with screenshots references
- **Configuration Details** — Both modes' configuration file structures
- **Directory Structure** — Complete folder layouts for both installations
- **Windows Service Management** — PowerShell commands for service control
- **Shortcuts & Launch Methods** — How to access dashboards and logs
- **Permissions** — NTFS and service account details
- **Uninstallation** — Data retention options
- **Troubleshooting** — Common issues and solutions
- **Updating** — How to upgrade to newer versions

---

### 3. Server Configuration Template (`InstallerNSIS\Payload\Server\appsettings.template.json`)

Reference template for payload preparation showing:
- `ListenUrl` — Default HTTP endpoint
- `DatabasePath` — Relative path to SQLite database
- `OnlineTimeoutMinutes` — Agent online timeout threshold

---

## Backward Compatibility

✅ **Fully backward compatible:**
- Existing Agent-only installations unaffected
- Agent is default selection (no behavior change for users selecting "Next")
- All original sections and functions preserved
- Registry key structure maintained

---

## Payload Directory Requirements

The installer expects the following payload structure:

```
Payload/
├── Service/
│   └── (StorageWatchService.exe and dependencies)
├── UI/
│   └── (StorageWatchUI.exe and dependencies)
├── Server/
│   ├── (StorageWatchServer.exe, wwwroot, Razor Pages)
│   └── appsettings.template.json
├── SQLite/
│   └── (SQLite runtime libraries)
├── Config/
│   └── StorageWatchConfig.json
└── Plugins/
    └── (Plugin DLLs)
```

---

## Installation Flow

### Agent Mode (Default)
```
Welcome → Components → Directory → Role Selection (Agent) → Install → Finish
```

### Central Server Mode
```
Welcome → Components → Directory → Role Selection (Server) → Server Config → Install → Finish
```

---

## Registry Entries

After installation, the following registry entries are created:

```
HKLM\Software\StorageWatch\
├── InstallDir = "$INSTDIR"
└── Role = "Agent" | "Server"
```

---

## Service Registration Details

### Agent Service
- **Service Name:** `StorageWatchService`
- **Display Name:** `StorageWatch Service`
- **Binary Path:** `$INSTDIR\Service\StorageWatchService.exe`
- **Start Type:** Automatic

### Server Service
- **Service Name:** `StorageWatchServer`
- **Display Name:** `StorageWatch Central Server`
- **Binary Path:** `$INSTDIR\Server\StorageWatchServer.exe`
- **Start Type:** Automatic

---

## Configuration Files

### Agent Configuration
- **Location:** `%PROGRAMDATA%\StorageWatch\Config\StorageWatchConfig.json`
- **Managed by:** Original Agent installer logic
- **User editable:** Yes (at runtime)

### Server Configuration
- **Location:** `$INSTDIR\Server\appsettings.json`
- **Generated at:** Installation time (by `GenerateServerConfig()`)
- **User inputs applied:**
  - Port number
  - Data directory path
- **User editable:** Yes (post-installation, with service restart required)

---

## Error Handling & Reliability

1. **Service Detection:** `.onInit` function detects existing installations and stops services before reinstall
2. **Permission Safety:** Uses `icacls` to set appropriate permissions for service operation
3. **Path Escaping:** NSIS variables properly escaped in JSON generation
4. **User Confirmation:** Prompts for data deletion during uninstall (non-destructive default)

---

## Testing Checklist

- [ ] Clean install as Agent mode → Service runs, UI launches
- [ ] Clean install as Server mode → Server service runs, dashboard accessible on configured port
- [ ] Role selection page displays correctly
- [ ] Server config page hidden in Agent mode
- [ ] Server config page shown and captures inputs in Server mode
- [ ] appsettings.json generated with correct values
- [ ] Start Menu shortcuts created appropriately for selected role
- [ ] Uninstall removes correct components and prompts for data deletion
- [ ] Reinstall detects and stops existing services
- [ ] Folder permissions applied correctly (Data/Logs writable by users)

---

## Notes for Build Process

The `Payload` directory must be populated before running the NSIS compiler. Recommended structure:

1. Publish `StorageWatchService` to `Payload\Service\`
2. Publish `StorageWatchUI` to `Payload\UI\`
3. Publish `StorageWatchServer` to `Payload\Server\` (includes wwwroot)
4. Copy SQLite runtime libraries to `Payload\SQLite\`
5. Copy config template to `Payload\Config\`
6. Copy plugins to `Payload\Plugins\`

Then run: `makensis InstallerNSIS\StorageWatchInstaller.nsi`

---

## Future Enhancements (Post-Step 14.5)

- [ ] Silent installation mode with command-line parameters
- [ ] Automatic port detection if default is in use
- [ ] SSL/TLS configuration in setup wizard
- [ ] Database migration helper for existing installations
- [ ] Auto-update mechanism integration
- [ ] Custom server name/identifier input
- [ ] Firewall rule configuration during install
