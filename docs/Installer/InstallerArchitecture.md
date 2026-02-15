# StorageWatch Installer Architecture

## Overview

The StorageWatch installer is built using **WiX Toolset v5.0**, a powerful and flexible toolchain for creating Windows Installer packages (.msi files). This document describes the architectural decisions, components, and design patterns used in the installer.

## Why WiX Toolset?

### Advantages
1. **Industry Standard**: WiX is the most widely used open-source installer technology for Windows
2. **MSI Format**: Produces standard Windows Installer packages with full OS integration
3. **Enterprise Ready**: Supports Group Policy deployment, repair, rollback, and administrative installs
4. **Declarative XML**: Clear, version-controllable installer definition
5. **Extensive Features**: Service installation, registry management, file associations, custom actions
6. **Active Community**: Well-documented with extensive examples and support

### Alternatives Considered
- **MSIX**: Modern packaging format, but limited service installation support and less flexible for upgrade scenarios
- **InstallShield**: Commercial, expensive, not open-source
- **Inno Setup**: Good for simple installers, but less enterprise-friendly than MSI

## Architecture Components

### 1. Project Structure

```
StorageWatchInstaller/
├── StorageWatchInstaller.wixproj    # WiX project file
├── Variables.wxi                     # Shared variables and constants
├── Package.wxs                       # Main installer definition
├── Components.wxs                    # File and component definitions
├── UI.wxs                           # Custom UI dialogs
├── License.rtf                      # CC0 license text
└── icon.ico                         # Application icon
```

### 2. WiX Files Explained

#### **Variables.wxi**
- Defines installer-wide constants (product name, version, GUIDs)
- Centralizes configuration for easy updates
- Included by all other WiX files

#### **Package.wxs**
- Root installer definition
- Defines package metadata (name, version, manufacturer)
- Specifies major upgrade behavior
- Declares features and custom actions
- Defines install/uninstall sequences

#### **Components.wxs**
- Defines all components (files, registry keys, services)
- Organizes components into logical groups:
  - ServiceComponents: Windows Service binaries
  - UIComponents: Desktop application files
  - ConfigComponents: Configuration files
  - PluginComponents: Plugin folder
  - StartMenuComponents: Start menu shortcuts

#### **UI.wxs**
- Custom installer wizard UI
- Based on WixUI_InstallDir template
- Customized dialogs for:
  - Installation folder selection
  - Desktop shortcut option
  - Service account configuration
  - Launch UI option

### 3. Component Architecture

#### Component Rules (WiX Best Practices)
1. **One Key Path Per Component**: Each component has exactly one key path (usually a file)
2. **Immutable Components**: GUIDs never change for a component; only version changes
3. **Component Grouping**: Related files grouped into ComponentGroups
4. **Shared Components**: Common dependencies shared across features

#### Component Groups

```xml
ServiceComponents
├── ServiceExecutable (StorageWatch.exe + ServiceInstall)
├── ServiceDependencies (DLLs)
└── SQLiteNative (native SQLite library)

UIComponents
├── UIExecutable (StorageWatchUI.exe)
└── UIDependencies (WPF + charting libraries)

ConfigComponents
└── ConfigFile (StorageWatchConfig.json, preserved on upgrade)

PluginComponents
└── PluginFolder (empty folder for future plugins)

StartMenuComponents
├── UIStartMenuShortcut
└── UninstallShortcut
```

### 4. Directory Structure

#### Installation Folders

| Directory | Variable | Purpose |
|-----------|----------|---------|
| `Program Files\StorageWatch` | `INSTALLFOLDER` | Service executable and dependencies |
| `Program Files\StorageWatch\UI` | `UIFOLDER` | Desktop application |
| `Program Files\StorageWatch\Plugins` | `PLUGINFOLDER` | Plugin assemblies |
| `ProgramData\StorageWatch` | `CONFIGFOLDER` | Configuration files |
| `ProgramData\StorageWatch\Logs` | `LOGSFOLDER` | Log files |
| `ProgramData\StorageWatch\Data` | `DATAFOLDER` | SQLite database |
| `Start Menu\Programs\StorageWatch` | `StartMenuFolder` | Shortcuts |
| `Desktop` | `DesktopFolder` | Optional desktop shortcut |

#### Why ProgramData for Configuration?

- **Permissions**: Writable by both service (LocalSystem) and UI (current user)
- **Persistence**: Survives user profile changes
- **Standards**: Microsoft best practice for shared application data
- **Backup-Friendly**: Easy to include in system backups

### 5. Windows Service Installation

```xml
<ServiceInstall Id="ServiceInstaller"
               Name="StorageWatchService"
               DisplayName="StorageWatch Service"
               Description="Monitors disk space and provides alerts"
               Type="ownProcess"
               Start="auto"
               Account="[SERVICEACCOUNT]"
               ErrorControl="normal" />

<ServiceControl Id="ServiceControl"
               Name="StorageWatchService"
               Start="install"
               Stop="both"
               Remove="uninstall"
               Wait="yes" />
```

**Features:**
- Installed as a Windows Service
- Automatic startup on boot
- Configurable service account (LocalSystem, LocalService, NetworkService)
- Graceful stop on upgrade/uninstall
- Service recovery options

### 6. Upgrade Strategy

#### Major Upgrade Logic

```xml
<MajorUpgrade DowngradeErrorMessage="A newer version is already installed."
              Schedule="afterInstallInitialize"
              AllowSameVersionUpgrades="yes" />
```

**Upgrade Sequence:**
1. Detect existing installation via `UpgradeCode`
2. Stop StorageWatch Service
3. Schedule old version for removal
4. Install new files
5. Preserve configuration and data (marked with `NeverOverwrite="yes"` and `Permanent="yes"`)
6. Start service with new version

**Preserved During Upgrade:**
- `StorageWatchConfig.json`
- SQLite database files in `ProgramData\StorageWatch\Data`
- Log files in `ProgramData\StorageWatch\Logs`

### 7. Custom Actions

Custom actions extend installer functionality:

#### **CreatePluginFolder / CreateLogsFolder / CreateDataFolder**
- Ensures folders exist in ProgramData
- Runs during installation (deferred, elevated)
- Idempotent (safe to run multiple times)

#### **SetPermissionsOnProgramData**
- Grants Users group modify permissions to ProgramData\StorageWatch
- Ensures UI can read/write config and database
- Uses `icacls.exe` for ACL management

#### **LaunchUI**
- Optionally launches StorageWatchUI after installation
- Runs as current user (not elevated)
- Async to avoid blocking installer completion

### 8. .NET Runtime Detection

```xml
<util:RegistrySearch Root="HKLM"
                    Key="SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost"
                    Value="Version"
                    Variable="DotNetVersion" />

<Launch Condition="DotNetVersion >= v10.0"
        Message="This application requires .NET 10.0 Runtime or later." />
```

**Validation:**
- Checks for .NET 10 runtime before installation
- Provides clear error message with download link
- Prevents installation failures due to missing dependencies

## Design Decisions

### 1. Single MSI vs Multiple Installers
**Decision**: Single MSI containing service + UI
**Rationale**: Simpler deployment, easier upgrades, consistent versioning

### 2. Configuration File Location
**Decision**: ProgramData instead of Program Files
**Rationale**: Writability, user access, Windows best practices

### 3. Service Account Default
**Decision**: LocalSystem with option to change
**Rationale**: Maximum compatibility, can access local resources, customizable for security

### 4. Upgrade vs Side-by-Side
**Decision**: In-place upgrades using MajorUpgrade
**Rationale**: Simpler for users, preserves data, standard Windows behavior

### 5. Uninstall Data Retention
**Decision**: Config/logs/database preserved by default during uninstall
**Rationale**: User data protection, prevents accidental data loss

### 6. Start Menu Structure
**Decision**: Single "StorageWatch" folder with shortcuts
**Rationale**: Clean organization, easy to find, includes uninstall shortcut

## Security Considerations

### 1. Service Account Isolation
- Default: LocalSystem (high privileges for disk access)
- Optional: LocalService or NetworkService (restricted)
- Future: Support for custom domain accounts

### 2. File Permissions
- Program Files: Read-only for non-admins (standard)
- ProgramData: Read/write for Users group
- Logs: Writable by service and UI

### 3. Secure Upgrade Path
- Old version stopped before new version installed
- No temporary exposure of mixed versions
- Service credentials preserved across upgrades

## Testing Strategy

### Installation Testing
- [ ] Clean install on fresh system
- [ ] Install with custom folder
- [ ] Install with custom service account
- [ ] Desktop shortcut created when selected
- [ ] Service starts automatically
- [ ] UI launches successfully

### Upgrade Testing
- [ ] Upgrade from version N to N+1
- [ ] Config file preserved
- [ ] Database preserved
- [ ] Service restarts with new version
- [ ] UI uses new binaries

### Uninstall Testing
- [ ] Service stopped and removed
- [ ] Binaries removed
- [ ] Shortcuts removed
- [ ] Config preserved (unless explicitly removed)
- [ ] Database preserved

### Edge Cases
- [ ] Install/uninstall/reinstall cycle
- [ ] Upgrade while service is running
- [ ] Upgrade while UI is open
- [ ] Install on system without .NET 10
- [ ] Install with insufficient permissions

## Future Enhancements

### Planned Features
1. **Server vs Agent Mode Selection**: Installer asks user intent
2. **Central Server Configuration**: Network settings during install
3. **Plugin Deployment**: Include official plugins in installer
4. **Certificate Installation**: For HTTPS central server
5. **Database Migration**: Automatic schema upgrades
6. **Rollback Support**: Automatic rollback on failed upgrade
7. **Silent Install**: Command-line parameters for automation
8. **Localization**: Multi-language support

### Technical Debt
- Replace placeholder icon.ico with actual icon
- Add signing certificate for trusted publisher
- Create Authenticode signature for binaries
- Add telemetry for installation success/failure rates

## Maintenance

### Updating the Installer

**Version Bump:**
1. Update `ProductVersion` in `Variables.wxi`
2. Keep `UpgradeCode` the same (enables upgrades)
3. WiX automatically generates new `ProductCode` for each version

**Adding New Files:**
1. Add `<File>` elements to appropriate ComponentGroup in `Components.wxs`
2. Use meaningful `Id` attributes
3. Ensure GUIDs are unique

**Modifying UI:**
1. Edit dialogs in `UI.wxs`
2. Follow WixUI standards for consistency
3. Test all navigation paths

### Build Process

```powershell
# Development build
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj

# Release build
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj -c Release

# Full rebuild
dotnet clean StorageWatchInstaller/StorageWatchInstaller.wixproj
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj -c Release
```

## References

- [WiX Toolset Documentation](https://wixtoolset.org/docs/)
- [Windows Installer Best Practices](https://docs.microsoft.com/en-us/windows/win32/msi/windows-installer-best-practices)
- [Component Rules](https://wixtoolset.org/docs/v3/howtos/general/componentrules/)
- [Service Installation](https://wixtoolset.org/docs/v3/xsd/wix/serviceinstall/)
