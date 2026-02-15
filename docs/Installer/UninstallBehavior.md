# StorageWatch Uninstall Behavior

This document describes what happens when StorageWatch is uninstalled, including what is removed, what is preserved, and how to perform a complete removal.

## 🗑️ Uninstall Methods

### Method 1: Settings App (Recommended)
```
Windows Settings
    → Apps
        → Installed apps
            → StorageWatch
                → Uninstall
```

### Method 2: Start Menu Shortcut
```
Start Menu
    → StorageWatch
        → Uninstall StorageWatch
```

### Method 3: Control Panel
```
Control Panel
    → Programs
        → Programs and Features
            → StorageWatch
                → Uninstall
```

### Method 4: Command Line (Silent)
```powershell
# Interactive uninstall
msiexec /x StorageWatchInstaller.msi

# Silent uninstall (keeps config)
msiexec /x StorageWatchInstaller.msi /quiet

# Silent uninstall with full removal (custom property)
msiexec /x StorageWatchInstaller.msi /quiet REMOVECONFIG=1 REMOVELOGS=1 REMOVEDATA=1
```

## 🔄 Uninstall Process Flow

### 1. Pre-Uninstall Actions

```
User Initiates Uninstall
    ↓
Windows Installer Starts
    ↓
Show Uninstall Dialog
    ↓
┌──────────────────────────────────┐
│ Remove StorageWatch?             │
│                                  │
│ This will remove the service     │
│ and application binaries.        │
│                                  │
│ ☐ Also remove configuration     │
│ ☐ Also remove logs               │
│ ☐ Also remove database           │
│                                  │
│  [ Yes ]  [ No ]                 │
└──────────────────────────────────┘
```

**User Choices:**
- **Default**: Remove binaries only, keep configuration/data
- **Optional**: Remove configuration, logs, and database

### 2. Stop StorageWatch Service

```powershell
sc.exe stop StorageWatchService
# Wait for graceful shutdown (max 30 seconds)
```

**Graceful Shutdown:**
- Service receives STOP signal
- Completes current disk scan (if running)
- Flushes pending database writes
- Closes log files
- Exits cleanly

**Force Shutdown (if needed):**
- After 30 seconds, service is forcibly terminated
- Windows Installer ensures all file locks released

### 3. Remove Windows Service Registration

```powershell
sc.exe delete StorageWatchService
```

**Removes:**
- Service registration from Services MMC
- Registry keys: `HKLM\SYSTEM\CurrentControlSet\Services\StorageWatchService`
- Event log source (if registered)

### 4. Remove Files & Folders

#### Always Removed

| Path | Type | Notes |
|------|------|-------|
| `C:\Program Files\StorageWatch\*.exe` | Files | Service and UI executables |
| `C:\Program Files\StorageWatch\*.dll` | Files | All dependencies |
| `C:\Program Files\StorageWatch\*.json` | Files | Runtime config files |
| `C:\Program Files\StorageWatch\UI\` | Folder | UI application folder |
| `C:\Program Files\StorageWatch\Plugins\` | Folder | Plugin folder (if empty) |
| `C:\Program Files\StorageWatch\` | Folder | Main installation folder |

**Note:** Plugin folder is only removed if empty. External plugins might prevent removal.

#### Conditionally Removed

| Path | Type | Removed When | Default |
|------|------|--------------|---------|
| `C:\ProgramData\StorageWatch\StorageWatchConfig.json` | File | User selects checkbox or `REMOVECONFIG=1` | ❌ Kept |
| `C:\ProgramData\StorageWatch\Logs\*.log` | Files | User selects checkbox or `REMOVELOGS=1` | ❌ Kept |
| `C:\ProgramData\StorageWatch\Data\*.db` | Files | User selects checkbox or `REMOVEDATA=1` | ❌ Kept |
| `C:\ProgramData\StorageWatch\` | Folder | If all files removed | Varies |

### 5. Remove Shortcuts

```
Remove:
✓ C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch\
  ├── StorageWatch Dashboard.lnk
  └── Uninstall StorageWatch.lnk

✓ C:\Users\[Username]\Desktop\StorageWatch Dashboard.lnk (if exists)
```

### 6. Remove Registry Entries

```
Remove:
✓ HKLM\SOFTWARE\StorageWatch Project\StorageWatch
✓ HKCU\SOFTWARE\StorageWatch
✓ HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode}
```

### 7. Clean Up System Locations

```
Check and remove if empty:
✓ C:\Program Files\StorageWatch\
✓ C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch\
```

### 8. Completion

```
┌──────────────────────────────────┐
│ Uninstall Complete               │
│                                  │
│ StorageWatch has been removed.   │
│                                  │
│ Configuration and data files     │
│ have been preserved in:          │
│ C:\ProgramData\StorageWatch\     │
│                                  │
│ To completely remove all data,   │
│ manually delete this folder.     │
│                                  │
│  [ OK ]                          │
└──────────────────────────────────┘
```

## 📋 What Gets Removed vs. Preserved

### ✅ Always Removed

1. **Binaries**
   - `StorageWatch.exe` (service)
   - `StorageWatchUI.exe` (desktop app)
   - All DLL dependencies
   - Native libraries (e.g., `e_sqlite3.dll`)

2. **Windows Service**
   - Service registration
   - Service control entries
   - Automatic startup entry

3. **Shortcuts**
   - Start Menu folder and shortcuts
   - Desktop shortcut (if created)
   - Uninstall shortcut

4. **Registry Keys**
   - Product registration keys
   - Installation location keys
   - Uninstaller keys

5. **Installation Folders**
   - `Program Files\StorageWatch\` (entire folder)
   - Start Menu program folder

### 🔒 Preserved by Default

1. **Configuration**
   - `C:\ProgramData\StorageWatch\StorageWatchConfig.json`
   - Contains user-customized settings
   - Preserved for potential reinstallation

2. **Database**
   - `C:\ProgramData\StorageWatch\Data\StorageWatch.db`
   - Contains historical monitoring data
   - Valuable for trend analysis

3. **Logs**
   - `C:\ProgramData\StorageWatch\Logs\*.log`
   - Useful for troubleshooting
   - May contain audit trail

**Rationale:** User data is precious. Accidental uninstalls shouldn't lose historical data.

## 🧹 Complete Removal (Optional)

### Automated Complete Removal

Use this command for a clean uninstall with full data removal:

```powershell
msiexec /x StorageWatchInstaller.msi /quiet REMOVECONFIG=1 REMOVELOGS=1 REMOVEDATA=1
```

### Manual Complete Removal

After uninstalling, manually delete:

```powershell
# Remove data folder
Remove-Item -Path "C:\ProgramData\StorageWatch" -Recurse -Force

# Remove any residual registry keys
Remove-Item -Path "HKCU:\SOFTWARE\StorageWatch" -Recurse -Force -ErrorAction SilentlyContinue

# Clean up Windows Installer cache (advanced)
# Get ProductCode from registry first
$productCode = "{12345678-1234-1234-1234-123456789012}"
Remove-Item -Path "C:\Windows\Installer\$productCode.msi" -Force -ErrorAction SilentlyContinue
```

### Verification

Verify complete removal:

```powershell
# Check binaries removed
Test-Path "C:\Program Files\StorageWatch" # Should return False

# Check service removed
Get-Service StorageWatchService -ErrorAction SilentlyContinue # Should return nothing

# Check data removed
Test-Path "C:\ProgramData\StorageWatch" # Should return False (if you deleted it)

# Check registry
Get-ItemProperty -Path "HKLM:\SOFTWARE\StorageWatch Project\StorageWatch" -ErrorAction SilentlyContinue # Should return nothing
```

## 🔄 Uninstall Scenarios

### Scenario 1: Standard Uninstall (Keep Data)

**User Action:** Uninstall via Settings, don't check "Remove data" boxes

**Result:**
- ✅ Binaries removed
- ✅ Service removed
- ✅ Shortcuts removed
- ❌ Configuration preserved
- ❌ Database preserved
- ❌ Logs preserved

**Disk Space Freed:** ~50 MB (binaries only)

**Use Case:** User wants to upgrade manually or temporarily remove the application

### Scenario 2: Clean Uninstall (Remove Everything)

**User Action:** Uninstall and check all "Remove" checkboxes

**Result:**
- ✅ Binaries removed
- ✅ Service removed
- ✅ Shortcuts removed
- ✅ Configuration removed
- ✅ Database removed
- ✅ Logs removed
- ✅ Empty parent folder removed

**Disk Space Freed:** ~100-200 MB (depending on data size)

**Use Case:** User is permanently removing StorageWatch

### Scenario 3: Failed Uninstall (Service Won't Stop)

**Symptom:** Uninstaller hangs or fails with "Service cannot be stopped"

**Resolution:**
1. Manually stop service:
   ```powershell
   Stop-Service StorageWatchService -Force
   ```
2. Retry uninstall
3. If still fails, reboot and retry

### Scenario 4: Partial Uninstall (Interrupted)

**Symptom:** Uninstaller crashed or was cancelled mid-process

**Resolution:**
1. Retry uninstall (Windows Installer has rollback capability)
2. If uninstaller is broken, use MSIEXEC with `/f` (repair) first:
   ```powershell
   msiexec /f StorageWatchInstaller.msi /quiet
   ```
3. Then retry uninstall:
   ```powershell
   msiexec /x StorageWatchInstaller.msi /quiet
   ```

### Scenario 5: Orphaned Files (Uninstaller Missing)

**Symptom:** User deleted installer MSI, can't uninstall via standard method

**Resolution:**
1. Obtain original MSI or download same version
2. Run MSI again (it will detect existing installation)
3. Use "Remove" option

**Alternative (Advanced):**
```powershell
# Find ProductCode in registry
$productCode = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | Where-Object { $_.DisplayName -eq "StorageWatch" } | Select-Object -ExpandProperty PSChildName

# Uninstall using ProductCode
msiexec /x $productCode /quiet
```

## ⚠️ Known Issues & Caveats

### Issue 1: Plugin Folder Not Removed
**Cause:** External plugin DLLs present in `Plugins\` folder
**Impact:** Parent folder `Program Files\StorageWatch\Plugins\` remains
**Resolution:** Manually delete or remove plugins first

### Issue 2: Service Account Permissions
**Cause:** Custom service account configured
**Impact:** ProgramData folder may have ACLs for that account
**Resolution:** Manually revoke ACLs after uninstall if needed

### Issue 3: File In Use During Uninstall
**Cause:** Antivirus or backup software scanning files
**Impact:** Uninstaller cannot remove some files
**Resolution:** Close interfering applications and retry

### Issue 4: Configuration Preserved Unexpectedly
**Cause:** User expected full removal but didn't check boxes
**Impact:** Config remains in ProgramData
**Resolution:** Manually delete or use command-line switches

## 📊 Uninstall Success Metrics

### Target Metrics
- **Successful Uninstall Rate:** > 99%
- **Average Time:** < 60 seconds
- **Files Left Behind (unintentional):** 0
- **Service Removal Success:** 100%

### Monitoring
- MSI logs: `%TEMP%\MSI*.log`
- Service status: `sc.exe query StorageWatchService`
- File system check: `Test-Path` PowerShell commands

## 🔧 Advanced Uninstall Options

### Silent Uninstall with Logging
```powershell
msiexec /x StorageWatchInstaller.msi /quiet /log "C:\Temp\uninstall.log"
```

### Force Uninstall (if standard method fails)
```powershell
# Stop service forcefully
taskkill /F /IM StorageWatch.exe /T
Stop-Service StorageWatchService -Force

# Remove service registration
sc.exe delete StorageWatchService

# Remove files manually
Remove-Item -Path "C:\Program Files\StorageWatch" -Recurse -Force

# Remove shortcuts manually
Remove-Item -Path "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch" -Recurse -Force
Remove-Item -Path "$env:USERPROFILE\Desktop\StorageWatch Dashboard.lnk" -Force -ErrorAction SilentlyContinue

# Remove registry keys manually
Remove-Item -Path "HKLM:\SOFTWARE\StorageWatch Project\StorageWatch" -Recurse -Force
Remove-Item -Path "HKCU:\SOFTWARE\StorageWatch" -Recurse -Force
```

## 🛡️ Uninstall Safety

### Protections in Place
1. **Confirmation Dialog**: User must confirm uninstall
2. **Data Preservation**: Default behavior keeps user data
3. **Graceful Service Stop**: Allows service to finish operations
4. **Rollback Capability**: Windows Installer can roll back failed uninstalls
5. **Clear Messaging**: User informed what will be removed vs. kept

### Risks
1. **Accidental Uninstall**: User intended to upgrade, not uninstall
2. **Data Loss**: If user checks "Remove all data" without backing up
3. **Disrupted Monitoring**: Service stops during uninstall

### Mitigation
- Backup recommendation in uninstall dialog
- Clear labeling of "Remove data" options
- Logs preserved by default for audit trail

## 📚 Related Documentation

- [Installer Architecture](InstallerArchitecture.md) - How uninstall is implemented
- [Upgrade Behavior](UpgradeBehavior.md) - Alternative to uninstall/reinstall
- [Folder Layout](FolderLayout.md) - What files exist where
- [Testing](Testing.md) - Uninstall test procedures
