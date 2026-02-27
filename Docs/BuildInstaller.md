# StorageWatch Installer Build Guide

## Prerequisites

- NSIS 3.x installed (from https://nsis.sourceforge.io/)
- .NET 10 SDK for publishing applications
- All StorageWatch projects compiled successfully

---

## Build Steps

### 1. Publish All Components

Publish each project to the `Payload` directory:

#### Agent Service
```powershell
dotnet publish StorageWatchAgent\StorageWatchAgent.csproj `
  -c Release -f net10.0 `
  -o InstallerNSIS\Payload\Agent\
```

#### Central Server
```powershell
dotnet publish StorageWatchServer\StorageWatchServer.csproj `
  -c Release -f net10.0 `
  -o InstallerNSIS\Payload\Server\
```

#### UI Application
```powershell
dotnet publish StorageWatchUI\StorageWatchUI.csproj `
  -c Release -f net10.0 `
  -o InstallerNSIS\Payload\UI\
```

### 2. Prepare SQLite Binaries

Copy required SQLite runtime files to:
```
InstallerNSIS\Payload\SQLite\
```

Include:
- `SQLite.Interop.dll` (x64)
- `sqlite3.dll`
- Any other required SQLite libraries per your NuGet dependencies

### 3. Prepare Configuration Templates

Ensure these files exist:

**Agent Config Template:**
```
InstallerNSIS\Payload\Config\StorageWatchConfig.json
```

**Server Config Template:**
```
InstallerNSIS\Payload\Server\appsettings.template.json
```

(Template is auto-expanded during installation with user selections)

### 4. Prepare Plugins Directory

```
InstallerNSIS\Payload\Plugins\
```

Copy any plugin DLLs (alert senders, etc.)

### 5. Build the Installer

Run NSIS to generate the installer executable:

```powershell
& "C:\Program Files (x86)\NSIS\makensis.exe" `
  "InstallerNSIS\StorageWatchInstaller.nsi"
```

Output: `InstallerNSIS\StorageWatchInstaller.exe`

---

## Complete Build Script

Save as `build-installer.ps1`:

```powershell
# Build and publish all components
Write-Host "Publishing StorageWatchAgent..."
dotnet publish StorageWatchAgent\StorageWatchAgent.csproj `
  -c Release -f net10.0 -o InstallerNSIS\Payload\Agent\

Write-Host "Publishing StorageWatchServer..."
dotnet publish StorageWatchServer\StorageWatchServer.csproj `
  -c Release -f net10.0 -o InstallerNSIS\Payload\Server\

Write-Host "Publishing StorageWatchUI..."
dotnet publish StorageWatchUI\StorageWatchUI.csproj `
  -c Release -f net10.0 -o InstallerNSIS\Payload\UI\

# Verify payload directories
$requiredDirs = @(
    "InstallerNSIS\Payload\Agent",
    "InstallerNSIS\Payload\Server",
    "InstallerNSIS\Payload\UI",
    "InstallerNSIS\Payload\SQLite",
    "InstallerNSIS\Payload\Config",
    "InstallerNSIS\Payload\Plugins"
)

foreach ($dir in $requiredDirs) {
    if (-not (Test-Path $dir)) {
        Write-Error "Missing payload directory: $dir"
        exit 1
    }
}

Write-Host "Verifying payload contents..."
if (-not (Test-Path "InstallerNSIS\Payload\Config\StorageWatchConfig.json")) {
    Write-Error "Missing StorageWatchConfig.json template"
    exit 1
}

if (-not (Test-Path "InstallerNSIS\Payload\Server\appsettings.template.json")) {
    Write-Error "Missing appsettings.template.json"
    exit 1
}

# Build the installer
Write-Host "Building StorageWatchInstaller.exe..."
$nsisPath = "C:\Program Files (x86)\NSIS\makensis.exe"

if (-not (Test-Path $nsisPath)) {
    Write-Error "NSIS not found at: $nsisPath"
    exit 1
}

& $nsisPath "InstallerNSIS\StorageWatchInstaller.nsi"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Installer built successfully: InstallerNSIS\StorageWatchInstaller.exe"
} else {
    Write-Error "NSIS build failed with exit code: $LASTEXITCODE"
    exit 1
}
```

Run with:
```powershell
.\build-installer.ps1
```

---

## Payload Directory Structure

The following structure must exist before running NSIS:

```
InstallerNSIS\Payload\
├── Agent\
│   ├── StorageWatchAgent.exe
│   ├── StorageWatchAgent.dll
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── (all dependencies)
│
├── Server\
│   ├── StorageWatchServer.exe
│   ├── StorageWatchServer.dll
│   ├── appsettings.template.json
│   ├── wwwroot\
│   │   ├── css\
│   │   ├── js\
│   │   └── (other assets)
│   ├── Dashboard\
│   │   ├── Index.cshtml
│   │   ├── Machines\
│   │   ├── Alerts.cshtml
│   │   └── Settings.cshtml
│   └── (all dependencies)
│
├── UI\
│   ├── StorageWatchUI.exe
│   ├── StorageWatchUI.dll
│   ├── appsettings.json
│   └── (all dependencies)
│
├── SQLite\
│   ├── SQLite.Interop.dll
│   ├── sqlite3.dll
│   └── (other SQLite files)
│
├── Config\
│   └── StorageWatchConfig.json
│
└── Plugins\
    └── (plugin DLLs)
```

---

## Testing the Installer

### Agent Mode Installation

1. Run `StorageWatchInstaller.exe`
2. Choose "Agent" on the Role Selection page
3. Skip the Server Config page
4. Install all components
5. Verify:
   - `StorageWatchAgent` service is registered and running
   - `StorageWatchUI.exe` can be launched from Start Menu
   - `%PROGRAMDATA%\StorageWatch\` directories created

### Central Server Installation

1. Run `StorageWatchInstaller.exe`
2. Choose "Central Server" on the Role Selection page
3. Enter port (e.g., 5001) and data directory
4. Install all components
5. Verify:
   - `StorageWatchServer` service is registered and running
   - `$INSTDIR\Server\appsettings.json` contains correct port and path
   - `http://localhost:5001` is accessible
   - Start Menu shortcuts created for "Central Dashboard" and "Server Logs"

### Uninstall Test

1. Run the uninstaller from Control Panel or `InstallerNSIS\Uninstall StorageWatch`
2. Verify prompts for:
   - Configuration deletion
   - Logs deletion
   - Data deletion (server database)
   - Plugins deletion
3. Verify services are removed

---

## Troubleshooting NSIS Build

### Error: "makensis not found"
- Verify NSIS installation path
- Update the path in the build script
- Or add NSIS to PATH environment variable

### Error: "File not found" in compilation
- Check payload directory structure
- Verify all published files are present
- Check for typos in `File` directives

### Installer runs but services don't start
- Verify `StorageWatchAgent.exe` and `StorageWatchServer.exe` are 64-bit
- Check Event Viewer → Windows Logs → Application for error details
- Verify .NET 10 runtime is installed on target machine

### Port already in use
- During server configuration, user should choose different port
- Or ensure previous installation is uninstalled

---

## Deployment

After building `StorageWatchInstaller.exe`:

1. Code sign the installer (optional but recommended for user trust)
2. Upload to release artifacts or GitHub Releases
3. Include SHA256 checksum for verification
4. Update documentation with download link and version number

---

## Continuous Integration

Example GitHub Actions workflow for building the installer:

```yaml
name: Build Installer

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Publish Services
        run: |
          dotnet publish StorageWatchAgent -c Release -f net10.0 -o InstallerNSIS\Payload\Agent
          dotnet publish StorageWatchServer -c Release -f net10.0 -o InstallerNSIS\Payload\Server
          dotnet publish StorageWatchUI -c Release -f net10.0 -o InstallerNSIS\Payload\UI
      
      - name: Install NSIS
        run: choco install nsis
      
      - name: Build Installer
        run: makensis InstallerNSIS\StorageWatchInstaller.nsi
      
      - name: Upload Artifact
        uses: actions/upload-artifact@v3
        with:
          name: StorageWatchInstaller
          path: InstallerNSIS\StorageWatchInstaller.exe
