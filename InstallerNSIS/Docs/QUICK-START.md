# StorageWatch Installer — Quick Start

## Prerequisites

- NSIS 3.x installed
- .NET 10 SDK
- All StorageWatch projects build successfully

---

## 1. Publish All Components

```powershell
# Agent Service
dotnet publish StorageWatchService\StorageWatchService.csproj `
  -c Release -f net10.0 `
  -o InstallerNSIS\Payload\Service\

# Central Server
dotnet publish StorageWatchServer\StorageWatchServer.csproj `
  -c Release -f net10.0 `
  -o InstallerNSIS\Payload\Server\

# Desktop UI
dotnet publish StorageWatchUI\StorageWatchUI.csproj `
  -c Release -f net10.0-windows `
  -o InstallerNSIS\Payload\UI\
```

## 2. Build the Installer

```powershell
makensis InstallerNSIS\StorageWatchInstaller.nsi
```

Output: `StorageWatchInstaller.exe`

---

## 3. Test

Run `StorageWatchInstaller.exe` and verify:
- Role selection page appears (Agent / Central Server)
- Service installs and starts
- Start Menu shortcuts are created
- Configuration files are in `%PROGRAMDATA%\StorageWatch\Config\`

---

## More Information

- [README.md](./README.md) — Full installer build documentation
- [Installer.md](../../Docs/Installer.md) — End-user installer guide
- [BuildInstaller.md](../../Docs/BuildInstaller.md) — Detailed build instructions
