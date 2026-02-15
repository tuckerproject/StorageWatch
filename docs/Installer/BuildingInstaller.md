# Building the StorageWatch Installer

This guide explains how to build the StorageWatch installer from source code.

## 📋 Prerequisites

### Required Software

1. **Visual Studio 2022** (v17.12 or later)
   - Workload: .NET desktop development
   - Download: https://visualstudio.microsoft.com/downloads/

2. **.NET 10 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation:
     ```powershell
     dotnet --version
     # Should show 10.0.x
     ```

3. **WiX Toolset v5.0 or later**
   - Download: https://wixtoolset.org/
   - Or install via command line:
     ```powershell
     dotnet tool install --global wix
     ```
   - Verify installation:
     ```powershell
     wix --version
     # Should show 5.0.x
     ```

4. **WiX Visual Studio Extension** (Optional, for IDE integration)
   - Download from: https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension
   - Or install via VS Extensions manager

### Optional Tools

- **7-Zip** or **WinRAR** - For examining MSI contents
- **Orca** - MSI table editor (from Windows SDK)
- **PowerShell 7+** - For build scripts

## 🏗️ Build Process Overview

```
Source Code
    ↓
Build Service Project
    ↓
Build UI Project
    ↓
Build Installer Project
    ↓
Generate MSI
```

## 🔧 Building from Visual Studio

### Step 1: Open Solution

```
File → Open → Solution/Project
Select: StorageWatch.slnx
```

### Step 2: Restore NuGet Packages

```
Tools → NuGet Package Manager → Package Manager Console
Run: dotnet restore
```

### Step 3: Set Build Configuration

```
Build Configuration: Release
Platform: x64
```

**Why x64?**
- StorageWatch targets 64-bit Windows
- WiX installer is configured for x64
- .NET 10 performs better on x64

### Step 4: Build Prerequisites

Build the dependencies first:

```powershell
# In Package Manager Console
dotnet build StorageWatch/StorageWatchService.csproj -c Release
dotnet build StorageWatchUI/StorageWatchUI.csproj -c Release
```

Or use Visual Studio:
```
Right-click StorageWatchService → Build
Right-click StorageWatchUI → Build
```

### Step 5: Build Installer

```
Right-click StorageWatchInstaller → Build
```

Or:
```
Build → Build Solution (Ctrl+Shift+B)
```

### Step 6: Locate Output

The MSI file will be created at:
```
StorageWatchInstaller\bin\Release\net10.0\StorageWatchInstaller.msi
```

## 💻 Building from Command Line

### Full Build Script (PowerShell)

```powershell
# Navigate to solution root
cd C:\Users\tucke\source\repos\StorageWatch

# Clean previous builds
dotnet clean

# Restore NuGet packages
dotnet restore

# Build Service
dotnet build StorageWatch/StorageWatchService.csproj `
    -c Release `
    -p:Platform=x64 `
    --no-restore

# Build UI
dotnet build StorageWatchUI/StorageWatchUI.csproj `
    -c Release `
    -p:Platform=x64 `
    --no-restore

# Build Installer
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj `
    -c Release `
    -p:Platform=x64 `
    --no-restore

Write-Host "✅ Build complete!"
Write-Host "📦 MSI location: StorageWatchInstaller\bin\Release\net10.0\StorageWatchInstaller.msi"
```

### Quick Build (Debug)

```powershell
# For development/testing
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj
```

## 🧪 Verifying the Build

### Check MSI Properties

```powershell
# Get MSI properties using PowerShell
$msi = "StorageWatchInstaller\bin\Release\net10.0\StorageWatchInstaller.msi"

# Check file exists and size
Get-Item $msi | Select-Object Name, Length, LastWriteTime

# Extract properties (requires Windows Installer COM)
$windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
$database = $windowsInstaller.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $windowsInstaller, @($msi, 0))
$view = $database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $database, @("SELECT * FROM Property"))
$view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)

# Display properties
while ($true) {
    $record = $view.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $view, $null)
    if ($record -eq $null) { break }
    $property = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 1)
    $value = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 2)
    Write-Host "$property = $value"
}
```

### Expected Properties

| Property | Value |
|----------|-------|
| ProductName | StorageWatch |
| ProductVersion | 1.0.0.0 |
| Manufacturer | StorageWatch Project |
| UpgradeCode | {12345678-1234-1234-1234-123456789012} |

### Validate MSI Structure

```powershell
# Extract MSI contents for inspection
msiexec /a StorageWatchInstaller.msi /qb TARGETDIR="C:\Temp\MSIExtract"

# Check extracted files
Get-ChildItem "C:\Temp\MSIExtract" -Recurse
```

Expected structure:
```
C:\Temp\MSIExtract\
├── ProgramFiles\
│   └── StorageWatch\
│       ├── StorageWatch.exe
│       └── UI\
│           └── StorageWatchUI.exe
└── ProgramData\
    └── StorageWatch\
        └── StorageWatchConfig.json
```

## 🐛 Troubleshooting Build Issues

### Issue 1: WiX Not Found

**Error:**
```
error MSB4226: The imported project "C:\Program Files\dotnet\sdk\10.0.100\Sdks\WixToolset.Sdk\Sdk\Sdk.props" was not found
```

**Solution:**
```powershell
# Install WiX globally
dotnet tool install --global wix

# Or add to project
dotnet add package WixToolset.Sdk
```

### Issue 2: Project Reference Failed

**Error:**
```
error WIX0001: Cannot find project reference 'StorageWatchService'
```

**Solution:**
Build dependencies first:
```powershell
dotnet build StorageWatch/StorageWatchService.csproj -c Release
dotnet build StorageWatchUI/StorageWatchUI.csproj -c Release
```

### Issue 3: Missing Dependencies

**Error:**
```
error WIX0103: File 'Microsoft.Data.Sqlite.dll' not found
```

**Solution:**
Ensure NuGet packages are restored:
```powershell
dotnet restore
dotnet build --no-restore
```

### Issue 4: Platform Mismatch

**Error:**
```
error MSB4057: The target "Build" does not exist in the project
```

**Solution:**
Specify platform explicitly:
```powershell
dotnet build -p:Platform=x64
```

### Issue 5: Icon Not Found

**Error:**
```
error LGHT0103: The system cannot find the file 'icon.ico'
```

**Solution:**
Replace placeholder `icon.ico` with actual icon file:
```powershell
# Copy existing icon or create one
Copy-Item "C:\Path\To\Real\Icon.ico" "StorageWatchInstaller\icon.ico"
```

### Issue 6: GUID Collision

**Error:**
```
error LGHT0130: The primary key 'XXX' is duplicated in table 'Component'
```

**Solution:**
Ensure all Component GUIDs are unique in `Components.wxs`. Generate new GUIDs:
```powershell
# PowerShell
[guid]::NewGuid().ToString().ToUpper()
```

## 📦 Advanced Build Options

### Custom Version Number

```powershell
# Override version during build
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj `
    -c Release `
    -p:ProductVersion=1.2.3.4
```

### Custom Output Name

```powershell
# Change output MSI name
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj `
    -c Release `
    -p:OutputName=StorageWatch_v1.0.0_x64
```

### Enable MSI Logging During Build

Add to `.wixproj`:
```xml
<PropertyGroup>
  <WixLoggingEnabled>true</WixLoggingEnabled>
  <WixLogLevel>verbose</WixLogLevel>
</PropertyGroup>
```

### Include PDB Files (Debugging)

```powershell
# Build with debug symbols
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj `
    -c Debug `
    -p:IncludeSymbols=true
```

## 🚀 Automated Build (CI/CD)

### GitHub Actions Workflow

Create `.github/workflows/build-installer.yml`:

```yaml
name: Build Installer

on:
  push:
    branches: [ main, release/* ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Install WiX
      run: dotnet tool install --global wix
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build Service
      run: dotnet build StorageWatch/StorageWatchService.csproj -c Release --no-restore
    
    - name: Build UI
      run: dotnet build StorageWatchUI/StorageWatchUI.csproj -c Release --no-restore
    
    - name: Build Installer
      run: dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj -c Release --no-restore
    
    - name: Upload MSI
      uses: actions/upload-artifact@v3
      with:
        name: StorageWatchInstaller
        path: StorageWatchInstaller/bin/Release/net10.0/*.msi
```

### Local Build Script

Create `build-installer.ps1`:

```powershell
<#
.SYNOPSIS
    Builds the StorageWatch installer MSI.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release
.PARAMETER Clean
    Perform a clean build. Default: False
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

Write-Host "🔨 Building StorageWatch Installer" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean
}

# Restore
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# Build Service
Write-Host "🔧 Building StorageWatch Service..." -ForegroundColor Yellow
dotnet build StorageWatch/StorageWatchService.csproj `
    -c $Configuration `
    -p:Platform=x64 `
    --no-restore

# Build UI
Write-Host "🖥️ Building StorageWatch UI..." -ForegroundColor Yellow
dotnet build StorageWatchUI/StorageWatchUI.csproj `
    -c $Configuration `
    -p:Platform=x64 `
    --no-restore

# Build Installer
Write-Host "📦 Building Installer..." -ForegroundColor Yellow
dotnet build StorageWatchInstaller/StorageWatchInstaller.wixproj `
    -c $Configuration `
    -p:Platform=x64 `
    --no-restore

$msiPath = "StorageWatchInstaller\bin\$Configuration\net10.0\StorageWatchInstaller.msi"

if (Test-Path $msiPath) {
    $msi = Get-Item $msiPath
    Write-Host ""
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host "📦 MSI: $($msi.FullName)" -ForegroundColor Cyan
    Write-Host "📊 Size: $([math]::Round($msi.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host "🕒 Modified: $($msi.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "❌ Build failed: MSI not found" -ForegroundColor Red
    exit 1
}
```

Usage:
```powershell
# Standard release build
.\build-installer.ps1

# Clean debug build
.\build-installer.ps1 -Configuration Debug -Clean
```

## 📝 Build Checklist

Before releasing an installer, verify:

- [ ] Version number updated in `Variables.wxi`
- [ ] All dependencies built successfully
- [ ] Icon file (`icon.ico`) is not a placeholder
- [ ] License.rtf contains correct license text
- [ ] MSI builds without errors
- [ ] MSI size is reasonable (~50-100 MB)
- [ ] Test install on clean VM
- [ ] Service starts after installation
- [ ] UI launches successfully
- [ ] Configuration file deployed correctly

## 📚 References

- [WiX Toolset Documentation](https://wixtoolset.org/docs/)
- [.NET CLI Build Commands](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- [MSBuild Reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reference)
- [Windows Installer Documentation](https://docs.microsoft.com/en-us/windows/win32/msi/windows-installer-portal)
