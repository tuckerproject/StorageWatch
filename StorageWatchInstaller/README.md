# StorageWatch Installer

Professional Windows Installer (MSI) package for StorageWatch, built with WiX Toolset v5.

## 🎯 What This Installer Does

- ✅ Installs **StorageWatch Service** (Windows Service for monitoring)
- ✅ Installs **StorageWatch Dashboard** (WPF desktop application)
- ✅ Registers Windows Service with automatic startup
- ✅ Deploys default configuration to `ProgramData`
- ✅ Creates Start Menu shortcuts
- ✅ Creates optional desktop shortcut
- ✅ Configures folders for logs, data, and plugins
- ✅ Supports seamless upgrades (preserves config and data)
- ✅ Supports clean uninstallation

## 🚀 Quick Start

### Build the Installer

```powershell
# From repository root
.\build-installer.ps1
```

The MSI will be created at:
```
StorageWatchInstaller\bin\Release\net10.0\StorageWatchInstaller.msi
```

### Install StorageWatch

```powershell
# Interactive install
.\StorageWatchInstaller.msi

# Silent install
msiexec /i StorageWatchInstaller.msi /quiet

# Silent install with log
msiexec /i StorageWatchInstaller.msi /quiet /log install.log
```

## 📋 Prerequisites

### For Building
- **.NET 10 SDK** - https://dotnet.microsoft.com/download/dotnet/10.0
- **WiX Toolset v5.0+** - `dotnet tool install --global wix`
- **Visual Studio 2022** (optional, for IDE integration)

### For Installing
- **Windows 10/11** or **Windows Server 2019/2022**
- **.NET 10 Runtime** - https://dotnet.microsoft.com/download/dotnet/10.0
- **Administrator privileges**

## 📦 What Gets Installed

### Binaries
```
C:\Program Files\StorageWatch\
├── StorageWatch.exe                 # Windows Service
├── [Service dependencies]
└── UI\
    ├── StorageWatchUI.exe           # Desktop Dashboard
    └── [UI dependencies]
```

### Configuration & Data
```
C:\ProgramData\StorageWatch\
├── StorageWatchConfig.json          # Configuration file
├── Data\
│   └── StorageWatch.db              # SQLite database
└── Logs\
    └── StorageWatch_YYYYMMDD.log    # Daily log files
```

### Shortcuts
- **Start Menu**: `StorageWatch Dashboard` and `Uninstall StorageWatch`
- **Desktop**: `StorageWatch Dashboard` (optional)

### Windows Service
- **Name**: `StorageWatchService`
- **Display Name**: `StorageWatch Service`
- **Startup**: Automatic
- **Account**: LocalSystem (configurable during install)

## 🔄 Upgrade Behavior

When upgrading to a newer version:

✅ **Preserved:**
- Configuration file (`StorageWatchConfig.json`)
- SQLite database with historical data
- Log files

✅ **Updated:**
- Service and UI binaries
- Dependencies and libraries
- Runtime configuration files

The installer automatically:
1. Stops the service
2. Replaces binaries
3. Restarts the service
4. Preserves all user data

## 🗑️ Uninstall Behavior

### Standard Uninstall (Default)
- ✅ Removes binaries
- ✅ Removes Windows Service
- ✅ Removes shortcuts
- ❌ **Preserves** configuration
- ❌ **Preserves** database
- ❌ **Preserves** logs

### Complete Uninstall
```powershell
msiexec /x StorageWatchInstaller.msi /quiet REMOVECONFIG=1 REMOVELOGS=1 REMOVEDATA=1
```

Removes everything including configuration and data.

## 🛠️ Build Options

### Standard Build
```powershell
.\build-installer.ps1
```

### Clean Build
```powershell
.\build-installer.ps1 -Clean
```

### Debug Build
```powershell
.\build-installer.ps1 -Configuration Debug
```

### Custom Version
```powershell
.\build-installer.ps1 -Version 1.2.3.4
```

### Skip Tests
```powershell
.\build-installer.ps1 -SkipTests
```

## 🧪 Testing

See [docs/Installer/Testing.md](../docs/Installer/Testing.md) for comprehensive testing procedures.

Quick validation:
```powershell
# Install
msiexec /i StorageWatchInstaller.msi /quiet

# Verify service
Get-Service StorageWatchService

# Verify binaries
Test-Path "C:\Program Files\StorageWatch\StorageWatch.exe"

# Verify config
Test-Path "C:\ProgramData\StorageWatch\StorageWatchConfig.json"

# Uninstall
msiexec /x StorageWatchInstaller.msi /quiet
```

## 📚 Documentation

Comprehensive documentation is available in `docs/Installer/`:

- **[README.md](../docs/Installer/README.md)** - Overview and quick start
- **[InstallerArchitecture.md](../docs/Installer/InstallerArchitecture.md)** - Technical architecture and design
- **[FolderLayout.md](../docs/Installer/FolderLayout.md)** - Complete folder structure
- **[UpgradeBehavior.md](../docs/Installer/UpgradeBehavior.md)** - How upgrades work
- **[UninstallBehavior.md](../docs/Installer/UninstallBehavior.md)** - Uninstallation process
- **[BuildingInstaller.md](../docs/Installer/BuildingInstaller.md)** - Build instructions
- **[Testing.md](../docs/Installer/Testing.md)** - Testing procedures

## 🔧 Project Structure

```
StorageWatchInstaller/
├── StorageWatchInstaller.wixproj    # WiX project file
├── Variables.wxi                     # Shared variables and constants
├── Package.wxs                       # Main installer definition
├── Components.wxs                    # File and component definitions
├── UI.wxs                           # Custom installer UI
├── License.rtf                      # CC0 license text
├── icon.ico                         # Application icon
└── README.md                        # This file
```

## ⚙️ Customization

### Change Product Version
Edit `Variables.wxi`:
```xml
<?define ProductVersion = "1.0.0.0" ?>
```

### Change Service Account
Edit `Package.wxs` or configure during installation:
```xml
<Property Id="SERVICEACCOUNT" Value="LocalSystem" />
```

### Change Install Path
Configure during installation or via command line:
```powershell
msiexec /i StorageWatchInstaller.msi INSTALLFOLDER="D:\CustomPath\StorageWatch"
```

### Disable Desktop Shortcut
```powershell
msiexec /i StorageWatchInstaller.msi INSTALLDESKTOPSHORTCUT=0
```

## ⚠️ Known Issues

1. **Icon Placeholder**: The `icon.ico` file is a placeholder. Replace with actual icon before distribution.
2. **GUIDs**: Component GUIDs are placeholders. Generate unique GUIDs for production.
3. **Code Signing**: MSI and binaries should be digitally signed for production use.

## 🛡️ Security Considerations

- Service runs as LocalSystem by default (high privileges)
- ProgramData folder has Users modify permissions (for UI access)
- Consider using LocalService or NetworkService for restricted environments
- Always download installer from official sources

## 📞 Support

- **Issues**: https://github.com/tuckerproject/DiskSpaceService/issues
- **Documentation**: https://github.com/tuckerproject/DiskSpaceService/tree/main/docs

## 📄 License

StorageWatch is released under **CC0 1.0 Universal (Public Domain Dedication)**.

The installer includes a full license notice in `License.rtf`, displayed during installation.

---

**Built with ❤️ using WiX Toolset**
