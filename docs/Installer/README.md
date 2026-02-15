# StorageWatch Installer Documentation

Welcome to the StorageWatch Installer documentation. This folder contains comprehensive information about the installer package, its architecture, behavior, and usage.

## 📚 Documentation Structure

- **[InstallerArchitecture.md](InstallerArchitecture.md)** - Technical architecture and design decisions
- **[FolderLayout.md](FolderLayout.md)** - Complete folder structure after installation
- **[UpgradeBehavior.md](UpgradeBehavior.md)** - How upgrades are handled
- **[UninstallBehavior.md](UninstallBehavior.md)** - Uninstallation process and data retention
- **[BuildingInstaller.md](BuildingInstaller.md)** - How to build the installer from source
- **[Testing.md](Testing.md)** - Testing procedures and checklists

## 🚀 Quick Start

### Prerequisites
- WiX Toolset v5.0 or later
- .NET 10 SDK
- Windows 10/11 or Windows Server 2019/2022

### Building the Installer

```powershell
# Build from Visual Studio
# Open StorageWatch.slnx
# Set StorageWatchInstaller as startup project
# Build Solution (Ctrl+Shift+B)

# Or build from command line
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj -c Release
```

The MSI file will be output to: `StorageWatchInstaller/bin/Release/net10.0/StorageWatchInstaller.msi`

### Installing StorageWatch

1. Run `StorageWatchInstaller.msi` as Administrator
2. Follow the installation wizard
3. Configure installation options:
   - Installation folder
   - Desktop shortcut preference
   - Service account (default: LocalSystem)
4. Click Install
5. Optionally launch StorageWatch Dashboard after installation

## 🎯 Key Features

### What Gets Installed
- ✅ **StorageWatch Service** - Background Windows Service for monitoring
- ✅ **StorageWatch Dashboard** - WPF desktop application
- ✅ **Configuration Files** - Default configuration in ProgramData
- ✅ **SQLite Database** - Local data storage
- ✅ **Start Menu Shortcuts** - Easy access to Dashboard
- ✅ **Service Registration** - Automatic startup on boot

### Upgrade Support
- ✅ Detects existing installations
- ✅ Preserves configuration files
- ✅ Preserves SQLite database and logs
- ✅ Updates binaries seamlessly
- ✅ Restarts service automatically

### Uninstall Support
- ✅ Stops and removes Windows Service
- ✅ Removes binaries and shortcuts
- ✅ Optionally preserves or removes:
  - Configuration files
  - SQLite database
  - Log files

## 📖 Detailed Documentation

For more detailed information, please refer to the individual documentation files:

- [Installer Architecture](InstallerArchitecture.md) - Learn about the WiX-based installer design
- [Folder Layout](FolderLayout.md) - Understand where files are installed
- [Upgrade Behavior](UpgradeBehavior.md) - Learn how upgrades work
- [Uninstall Behavior](UninstallBehavior.md) - Understand what happens during uninstallation
- [Building the Installer](BuildingInstaller.md) - Step-by-step build instructions
- [Testing](Testing.md) - Complete testing procedures

## 🛠️ Troubleshooting

### Common Issues

**Issue**: Installer fails with "Another version is already installed"
**Solution**: Uninstall the previous version first, or use the upgrade installer

**Issue**: Service fails to start after installation
**Solution**: Check Event Viewer for errors, verify .NET 10 Runtime is installed

**Issue**: Configuration file not found
**Solution**: Check `C:\ProgramData\StorageWatch\StorageWatchConfig.json` exists

**Issue**: Permission denied errors
**Solution**: Ensure the service account has appropriate permissions to ProgramData folder

## 📞 Support

For issues or questions:
- GitHub Issues: https://github.com/tuckerproject/DiskSpaceService/issues
- Documentation: https://github.com/tuckerproject/DiskSpaceService/tree/main/docs

## 📄 License

StorageWatch is released under CC0 1.0 Universal (Public Domain Dedication).
See the License.rtf file in the installer for full details.
