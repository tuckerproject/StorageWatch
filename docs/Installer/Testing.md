# StorageWatch Installer Testing

Comprehensive testing procedures and checklists for the StorageWatch installer.

## 🎯 Testing Objectives

1. **Clean Install**: Verify installer works on fresh systems
2. **Upgrade Install**: Ensure smooth upgrades from previous versions
3. **Uninstall**: Confirm complete and safe removal
4. **Service Functionality**: Validate service starts and operates correctly
5. **UI Functionality**: Verify UI launches and functions post-install
6. **Configuration**: Ensure config files deployed and preserved
7. **Edge Cases**: Test failure scenarios and recovery

## 🧪 Test Environment Requirements

### Test Machines

| Environment | Purpose | OS | Notes |
|-------------|---------|---|-------|
| Clean VM #1 | Fresh install | Windows 10 22H2 | No StorageWatch previously installed |
| Clean VM #2 | Fresh install | Windows 11 23H2 | Latest Windows release |
| Clean VM #3 | Fresh install | Windows Server 2022 | Server environment |
| Upgrade VM | Upgrade testing | Windows 10 | Previous StorageWatch version installed |
| Dirty VM | Edge cases | Windows 11 | Antivirus, limited permissions |

### Required Tools

- **Virtual Machine Software**: Hyper-V, VMware, or VirtualBox
- **Snapshots**: Take before/after snapshots
- **PowerShell**: For automation
- **Sysinternals Suite**: For process monitoring
- **Event Viewer**: For error logging
- **Services MMC**: For service verification

## 📋 Test Procedures

### Test 1: Clean Installation

**Objective:** Verify installer works on a system without StorageWatch

#### Prerequisites
- [ ] Fresh Windows installation (or snapshot restore)
- [ ] .NET 10 Runtime installed
- [ ] Administrator privileges
- [ ] Latest MSI installer available

#### Test Steps

1. **Launch Installer**
   ```powershell
   # Run MSI
   .\StorageWatchInstaller.msi
   ```
   - [ ] Installer UI appears
   - [ ] Welcome screen displays correct product name and version
   - [ ] No error messages

2. **License Agreement**
   - [ ] License text displays (CC0)
   - [ ] "I accept" checkbox appears
   - [ ] Cannot proceed without accepting

3. **Installation Options**
   - [ ] Default install path: `C:\Program Files\StorageWatch`
   - [ ] Browse button functional
   - [ ] Desktop shortcut checkbox (checked by default)
   - [ ] Service account dropdown (LocalSystem default)

4. **Installation Progress**
   - [ ] Progress bar animates
   - [ ] Status messages appear (e.g., "Copying files...")
   - [ ] No errors or warnings

5. **Completion Screen**
   - [ ] Success message appears
   - [ ] "Launch StorageWatch Dashboard" checkbox
   - [ ] Clicking Finish closes installer

6. **Post-Install Verification**
   ```powershell
   # Check service exists
   Get-Service StorageWatchService
   # Status should be: Running
   
   # Check service is automatic
   Get-Service StorageWatchService | Select-Object StartType
   # Should be: Automatic
   
   # Check binaries exist
   Test-Path "C:\Program Files\StorageWatch\StorageWatch.exe"
   Test-Path "C:\Program Files\StorageWatch\UI\StorageWatchUI.exe"
   
   # Check config deployed
   Test-Path "C:\ProgramData\StorageWatch\StorageWatchConfig.json"
   
   # Check folders created
   Test-Path "C:\ProgramData\StorageWatch\Logs"
   Test-Path "C:\ProgramData\StorageWatch\Data"
   Test-Path "C:\Program Files\StorageWatch\Plugins"
   
   # Check shortcuts
   Test-Path "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch\StorageWatch Dashboard.lnk"
   Test-Path "$env:USERPROFILE\Desktop\StorageWatch Dashboard.lnk"
   
   # Check registry
   Get-ItemProperty "HKLM:\SOFTWARE\StorageWatch Project\StorageWatch"
   ```

7. **Service Functionality**
   ```powershell
   # Check service status
   sc.exe query StorageWatchService
   
   # Verify service can be stopped
   Stop-Service StorageWatchService
   Start-Service StorageWatchService
   
   # Check logs for startup messages
   Get-Content "C:\ProgramData\StorageWatch\Logs\StorageWatch_$(Get-Date -Format yyyyMMdd).log" -Tail 20
   ```

8. **UI Launch**
   - [ ] Double-click Start Menu shortcut
   - [ ] UI launches without errors
   - [ ] Dashboard displays disk information
   - [ ] Service status shows "Running"

#### Expected Results
- ✅ All checks pass
- ✅ Service running
- ✅ UI functional
- ✅ No errors in Event Viewer

#### Cleanup
```powershell
# Uninstall for next test
msiexec /x StorageWatchInstaller.msi /quiet

# Restore VM snapshot if needed
```

---

### Test 2: Upgrade Installation

**Objective:** Verify installer upgrades existing installations correctly

#### Prerequisites
- [ ] Previous version of StorageWatch installed (e.g., v1.0.0)
- [ ] Service is running
- [ ] Configuration has been customized
- [ ] Historical data exists in database

#### Setup
```powershell
# Install version 1.0.0
.\StorageWatchInstaller_v1.0.0.msi /quiet

# Customize configuration
$config = Get-Content "C:\ProgramData\StorageWatch\StorageWatchConfig.json" | ConvertFrom-Json
$config.StorageWatch.Monitoring.ThresholdPercent = 15
$config.StorageWatch.Monitoring.Drives = @("C:", "D:", "E:")
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\ProgramData\StorageWatch\StorageWatchConfig.json"

# Let service run for 5 minutes to generate data
Start-Sleep -Seconds 300

# Backup config for comparison
Copy-Item "C:\ProgramData\StorageWatch\StorageWatchConfig.json" "C:\Temp\config.backup.json"

# Check database size
Get-Item "C:\ProgramData\StorageWatch\Data\StorageWatch.db" | Select-Object Length
```

#### Test Steps

1. **Launch Upgrade Installer**
   ```powershell
   # Run newer version MSI
   .\StorageWatchInstaller_v1.1.0.msi
   ```
   - [ ] Installer detects existing installation
   - [ ] Shows "Upgrading StorageWatch" message (not "Installing")

2. **Upgrade Progress**
   - [ ] Service stops gracefully
   - [ ] Progress bar advances
   - [ ] No "file in use" errors

3. **Post-Upgrade Verification**
   ```powershell
   # Check version updated
   (Get-Item "C:\Program Files\StorageWatch\StorageWatch.exe").VersionInfo.FileVersion
   # Should be: 1.1.0.0
   
   # Check service running
   Get-Service StorageWatchService
   # Status: Running
   
   # Check config preserved
   $newConfig = Get-Content "C:\ProgramData\StorageWatch\StorageWatchConfig.json" | ConvertFrom-Json
   $newConfig.StorageWatch.Monitoring.ThresholdPercent
   # Should be: 15 (custom value preserved)
   
   # Check database preserved
   Test-Path "C:\ProgramData\StorageWatch\Data\StorageWatch.db"
   # Should exist with same or larger size
   
   # Check logs preserved
   Get-ChildItem "C:\ProgramData\StorageWatch\Logs"
   # Old logs should still exist
   
   # Compare configs
   Compare-Object (Get-Content "C:\Temp\config.backup.json") (Get-Content "C:\ProgramData\StorageWatch\StorageWatchConfig.json")
   # Should show no differences in preserved fields
   ```

4. **Functionality After Upgrade**
   - [ ] Launch UI
   - [ ] Historical data visible in trends
   - [ ] Configuration settings intact
   - [ ] Service monitoring correctly

#### Expected Results
- ✅ Binaries updated to new version
- ✅ Configuration preserved with custom values
- ✅ Database intact with historical data
- ✅ Logs preserved
- ✅ Service automatically restarted
- ✅ No data loss

---

### Test 3: Uninstall

**Objective:** Verify clean uninstallation

#### Prerequisites
- [ ] StorageWatch installed
- [ ] Service running
- [ ] UI closed

#### Test Steps (Standard Uninstall)

1. **Launch Uninstaller**
   ```powershell
   # Via Settings
   # Settings → Apps → StorageWatch → Uninstall
   
   # Or via command line
   msiexec /x StorageWatchInstaller.msi
   ```

2. **Uninstall Options**
   - [ ] Dialog shows uninstall options
   - [ ] Checkboxes for removing config/logs/data (all unchecked by default)
   - [ ] Warning about data preservation

3. **Uninstall Progress**
   - [ ] Service stops
   - [ ] Progress bar completes
   - [ ] Success message

4. **Post-Uninstall Verification**
   ```powershell
   # Check service removed
   Get-Service StorageWatchService -ErrorAction SilentlyContinue
   # Should return nothing
   
   # Check binaries removed
   Test-Path "C:\Program Files\StorageWatch"
   # Should be: False
   
   # Check shortcuts removed
   Test-Path "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch"
   # Should be: False
   Test-Path "$env:USERPROFILE\Desktop\StorageWatch Dashboard.lnk"
   # Should be: False
   
   # Check config PRESERVED (default)
   Test-Path "C:\ProgramData\StorageWatch\StorageWatchConfig.json"
   # Should be: True
   
   # Check database PRESERVED
   Test-Path "C:\ProgramData\StorageWatch\Data\StorageWatch.db"
   # Should be: True
   
   # Check registry removed
   Get-ItemProperty "HKLM:\SOFTWARE\StorageWatch Project\StorageWatch" -ErrorAction SilentlyContinue
   # Should return nothing
   ```

#### Test Steps (Complete Removal)

1. **Reinstall**
   ```powershell
   .\StorageWatchInstaller.msi /quiet
   ```

2. **Uninstall with Data Removal**
   ```powershell
   msiexec /x StorageWatchInstaller.msi /quiet REMOVECONFIG=1 REMOVELOGS=1 REMOVEDATA=1
   ```

3. **Verify Complete Removal**
   ```powershell
   # Everything should be gone
   Test-Path "C:\Program Files\StorageWatch" # False
   Test-Path "C:\ProgramData\StorageWatch" # False
   Get-Service StorageWatchService -ErrorAction SilentlyContinue # Nothing
   ```

#### Expected Results
- ✅ Standard uninstall: Binaries removed, data preserved
- ✅ Complete uninstall: Everything removed
- ✅ No orphaned files or registry keys
- ✅ Service completely removed

---

### Test 4: Edge Cases

#### Test 4a: Install Without .NET Runtime

**Steps:**
1. Remove .NET 10 Runtime
2. Run installer
3. **Expected:** Error message about missing .NET 10, link to download

#### Test 4b: Install with Insufficient Permissions

**Steps:**
1. Run installer as standard user (not admin)
2. **Expected:** UAC prompt or error about admin rights required

#### Test 4c: Upgrade While UI Is Running

**Steps:**
1. Install v1.0.0
2. Launch UI
3. Run v1.1.0 installer
4. **Expected:** Prompt to close UI before continuing

#### Test 4d: Upgrade While Service Is Processing

**Steps:**
1. Install v1.0.0
2. Trigger disk scan (modify config to run frequently)
3. Run v1.1.0 installer mid-scan
4. **Expected:** Service stops gracefully, completes scan, then upgrades

#### Test 4e: Disk Full During Install

**Steps:**
1. Fill C: drive to < 50 MB free
2. Run installer
3. **Expected:** Error about insufficient disk space

#### Test 4f: Install to Non-Default Path

**Steps:**
1. Run installer
2. Change install path to `D:\CustomFolder\StorageWatch`
3. **Expected:** Installs to custom path, all shortcuts work

#### Test 4g: Interrupted Install (Power Loss)

**Steps:**
1. Start installer
2. Terminate installer process mid-install (`taskkill /F /IM msiexec.exe`)
3. **Expected:** Windows Installer rollback, system left in pre-install state
4. Retry installer
5. **Expected:** Successful install on retry

#### Test 4h: Antivirus Interference

**Steps:**
1. Enable aggressive antivirus (e.g., Windows Defender with high settings)
2. Run installer
3. **Expected:** May prompt for permission, but completes successfully

#### Test 4i: Multiple Simultaneous Installs

**Steps:**
1. Run installer
2. While running, attempt second installer instance
3. **Expected:** Windows Installer blocks second instance with "Another installation is in progress"

---

## 🔍 Validation Scripts

### Quick Validation Script

```powershell
<#
.SYNOPSIS
    Validates StorageWatch installation.
#>

Write-Host "🔍 Validating StorageWatch Installation" -ForegroundColor Cyan

$errors = @()

# Check service
Write-Host "Checking service..." -NoNewline
$service = Get-Service StorageWatchService -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq 'Running') {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Service not running"
}

# Check binaries
Write-Host "Checking binaries..." -NoNewline
if ((Test-Path "C:\Program Files\StorageWatch\StorageWatch.exe") -and 
    (Test-Path "C:\Program Files\StorageWatch\UI\StorageWatchUI.exe")) {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Binaries missing"
}

# Check config
Write-Host "Checking configuration..." -NoNewline
if (Test-Path "C:\ProgramData\StorageWatch\StorageWatchConfig.json") {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Configuration missing"
}

# Check folders
Write-Host "Checking folders..." -NoNewline
if ((Test-Path "C:\ProgramData\StorageWatch\Logs") -and 
    (Test-Path "C:\ProgramData\StorageWatch\Data") -and 
    (Test-Path "C:\Program Files\StorageWatch\Plugins")) {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Folders missing"
}

# Check shortcuts
Write-Host "Checking shortcuts..." -NoNewline
if (Test-Path "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StorageWatch\StorageWatch Dashboard.lnk") {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Shortcuts missing"
}

# Check registry
Write-Host "Checking registry..." -NoNewline
$regKey = Get-ItemProperty "HKLM:\SOFTWARE\StorageWatch Project\StorageWatch" -ErrorAction SilentlyContinue
if ($regKey) {
    Write-Host " ✅" -ForegroundColor Green
} else {
    Write-Host " ❌" -ForegroundColor Red
    $errors += "Registry keys missing"
}

# Summary
Write-Host ""
if ($errors.Count -eq 0) {
    Write-Host "✅ All checks passed! Installation is valid." -ForegroundColor Green
} else {
    Write-Host "❌ Installation has issues:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
```

## 📊 Test Results Template

### Test Report: StorageWatch Installer v1.0.0

**Date:** 2025-01-01
**Tester:** [Name]
**Environment:** Windows 11 23H2, .NET 10.0.0

| Test | Result | Notes |
|------|--------|-------|
| Clean Install | ✅ Pass | Installed successfully |
| Upgrade (1.0→1.1) | ✅ Pass | Config preserved |
| Uninstall (Standard) | ✅ Pass | Data preserved |
| Uninstall (Complete) | ✅ Pass | All files removed |
| Service Start | ✅ Pass | Started automatically |
| UI Launch | ✅ Pass | No errors |
| Config Deployed | ✅ Pass | JSON valid |
| Database Created | ✅ Pass | SQLite functional |
| Shortcuts | ✅ Pass | Start Menu + Desktop |
| Edge Case: No .NET | ✅ Pass | Clear error message |
| Edge Case: Non-Admin | ✅ Pass | UAC prompt appeared |
| Edge Case: Upgrade with UI Open | ❌ Fail | Installer hung |

**Issues Found:**
- Issue #1: Installer does not prompt to close UI before upgrade

**Overall:** 11/12 tests passed (92%)

## 📝 Testing Checklist

### Pre-Release Testing

- [ ] Clean install on Windows 10
- [ ] Clean install on Windows 11
- [ ] Clean install on Windows Server 2022
- [ ] Upgrade from previous version
- [ ] Downgrade blocked correctly
- [ ] Uninstall (standard)
- [ ] Uninstall (complete removal)
- [ ] Install without .NET Runtime (error message)
- [ ] Install with custom path
- [ ] Install with custom service account
- [ ] Desktop shortcut optional
- [ ] Service starts automatically
- [ ] UI launches successfully
- [ ] Configuration valid
- [ ] Database created
- [ ] Logs directory created
- [ ] Plugins folder created
- [ ] Start Menu shortcuts work
- [ ] Uninstall shortcut works
- [ ] Registry keys correct
- [ ] Permissions on ProgramData correct
- [ ] Upgrade preserves config
- [ ] Upgrade preserves database
- [ ] Upgrade preserves logs
- [ ] No errors in Event Viewer

### Regression Testing

- [ ] All previous test cases still pass
- [ ] No new issues introduced
- [ ] Performance acceptable (install < 2 min)
- [ ] MSI size reasonable (< 100 MB)

## 📚 Related Documentation

- [Installer Architecture](InstallerArchitecture.md)
- [Upgrade Behavior](UpgradeBehavior.md)
- [Uninstall Behavior](UninstallBehavior.md)
- [Building Installer](BuildingInstaller.md)
