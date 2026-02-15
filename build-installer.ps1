<#
.SYNOPSIS
    Builds the StorageWatch installer MSI package.

.DESCRIPTION
    This script automates the build process for the StorageWatch installer.
    It builds the service, UI, and installer projects in the correct order,
    validates the output, and provides helpful information about the result.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default is Release.

.PARAMETER Clean
    Performs a clean build by removing all previous build artifacts.

.PARAMETER SkipTests
    Skips running tests before building the installer.

.PARAMETER Version
    Override the product version. Format: Major.Minor.Build.Revision

.EXAMPLE
    .\build-installer.ps1
    Builds the installer in Release configuration.

.EXAMPLE
    .\build-installer.ps1 -Configuration Debug -Clean
    Performs a clean debug build.

.EXAMPLE
    .\build-installer.ps1 -Version 1.2.3.4
    Builds with a custom version number.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$Clean,
    
    [Parameter()]
    [switch]$SkipTests,
    
    [Parameter()]
    [string]$Version = $null
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Banner
Write-Host ""
Write-Host "╔═══════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   StorageWatch Installer Build Script    ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Display build configuration
Write-Host "Configuration: " -NoNewline -ForegroundColor Gray
Write-Host $Configuration -ForegroundColor White
if ($Version) {
    Write-Host "Version:       " -NoNewline -ForegroundColor Gray
    Write-Host $Version -ForegroundColor White
}
Write-Host ""

# Verify prerequisites
Write-Host "🔍 Checking prerequisites..." -ForegroundColor Yellow

# Check .NET SDK
try {
    $dotnetVersion = & dotnet --version
    Write-Host "  ✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ .NET SDK not found. Please install .NET 10 SDK." -ForegroundColor Red
    Write-Host "    Download from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    exit 1
}

# Check WiX Toolset
try {
    $wixVersion = & wix --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ WiX Toolset: $wixVersion" -ForegroundColor Green
    } else {
        throw "WiX not found"
    }
} catch {
    Write-Host "  ✗ WiX Toolset not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global wix
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    Failed to install WiX. Please install manually." -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ WiX Toolset installed" -ForegroundColor Green
}

Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Clean failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Clean complete" -ForegroundColor Green
    Write-Host ""
}

# Restore NuGet packages
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Packages restored" -ForegroundColor Green
Write-Host ""

# Run tests (unless skipped)
if (-not $SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test --configuration $Configuration --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Tests failed" -ForegroundColor Red
        Write-Host "    Use -SkipTests to bypass test failures" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  ✓ All tests passed" -ForegroundColor Green
    Write-Host ""
}

# Build Service
Write-Host "🔧 Building StorageWatch Service..." -ForegroundColor Yellow
$serviceArgs = @(
    'build'
    'StorageWatch/StorageWatchService.csproj'
    '--configuration', $Configuration
    '--no-restore'
    '--verbosity', 'quiet'
    '/p:Platform=x64'
)
if ($Version) {
    $serviceArgs += "/p:Version=$Version"
}
& dotnet $serviceArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Service build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Service built successfully" -ForegroundColor Green

# Build UI
Write-Host "🖥️  Building StorageWatch UI..." -ForegroundColor Yellow
$uiArgs = @(
    'build'
    'StorageWatchUI/StorageWatchUI.csproj'
    '--configuration', $Configuration
    '--no-restore'
    '--verbosity', 'quiet'
    '/p:Platform=x64'
)
if ($Version) {
    $uiArgs += "/p:Version=$Version"
}
& dotnet $uiArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ UI build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ UI built successfully" -ForegroundColor Green

# Build Installer
Write-Host "📦 Building Installer..." -ForegroundColor Yellow
$installerArgs = @(
    'build'
    'StorageWatchInstaller/StorageWatchInstaller.wixproj'
    '--configuration', $Configuration
    '--no-restore'
    '--verbosity', 'quiet'
    '/p:Platform=x64'
)
if ($Version) {
    $installerArgs += "/p:ProductVersion=$Version"
}
& dotnet $installerArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Installer build failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  • Ensure icon.ico is a valid icon file (not a placeholder)" -ForegroundColor Gray
    Write-Host "  • Check that all GUIDs in Components.wxs are unique" -ForegroundColor Gray
    Write-Host "  • Verify WiX extension packages are installed" -ForegroundColor Gray
    exit 1
}
Write-Host "  ✓ Installer built successfully" -ForegroundColor Green
Write-Host ""

# Validate output
$msiPath = Join-Path $PSScriptRoot "StorageWatchInstaller\bin\$Configuration\net10.0\StorageWatchInstaller.msi"

if (Test-Path $msiPath) {
    $msi = Get-Item $msiPath
    $msiSizeMB = [math]::Round($msi.Length / 1MB, 2)
    
    Write-Host "╔═══════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║          Build Successful! ✅             ║" -ForegroundColor Green
    Write-Host "╚═══════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Installer Details:" -ForegroundColor Cyan
    Write-Host "   Path:     " -NoNewline -ForegroundColor Gray
    Write-Host $msi.FullName -ForegroundColor White
    Write-Host "   Size:     " -NoNewline -ForegroundColor Gray
    Write-Host "$msiSizeMB MB" -ForegroundColor White
    Write-Host "   Modified: " -NoNewline -ForegroundColor Gray
    Write-Host $msi.LastWriteTime -ForegroundColor White
    Write-Host ""
    
    # Quick validation
    Write-Host "🔍 Quick Validation:" -ForegroundColor Cyan
    
    # Check MSI can be opened
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $installer, @($msiPath, 0))
        
        # Read ProductName and ProductVersion
        $view = $database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $database, @("SELECT Value FROM Property WHERE Property='ProductName'"))
        $view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)
        $record = $view.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $view, $null)
        $productName = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 1)
        
        $view = $database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $database, @("SELECT Value FROM Property WHERE Property='ProductVersion'"))
        $view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)
        $record = $view.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $view, $null)
        $productVersion = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 1)
        
        Write-Host "   Product:  " -NoNewline -ForegroundColor Gray
        Write-Host $productName -ForegroundColor White
        Write-Host "   Version:  " -NoNewline -ForegroundColor Gray
        Write-Host $productVersion -ForegroundColor White
        Write-Host "   ✓ MSI structure valid" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠ Could not validate MSI properties" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "📝 Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Test install on a clean VM" -ForegroundColor Gray
    Write-Host "   2. Verify service starts automatically" -ForegroundColor Gray
    Write-Host "   3. Test UI launches successfully" -ForegroundColor Gray
    Write-Host "   4. Test upgrade from previous version" -ForegroundColor Gray
    Write-Host "   5. Test uninstall and data preservation" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📚 Documentation: docs/Installer/" -ForegroundColor Cyan
    Write-Host ""
    
} else {
    Write-Host "❌ Build failed: MSI not found at expected location" -ForegroundColor Red
    Write-Host "   Expected: $msiPath" -ForegroundColor Yellow
    exit 1
}
