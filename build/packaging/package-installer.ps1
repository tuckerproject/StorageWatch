[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter()]
    [ValidateSet('stable','prerelease')]
    [string]$Channel = 'stable',

    [Parameter()]
    [string]$OutputRoot = (Join-Path $RepoRoot 'artifacts'),

    [Parameter()]
    [string]$PayloadRoot = (Join-Path $RepoRoot 'InstallerNSIS\Payload'),

    [Parameter()]
    [string]$NsisScriptPath = (Join-Path $RepoRoot 'InstallerNSIS\StorageWatchInstaller.nsi'),

    [Parameter()]
    [string]$NsisExePath = 'makensis',

    [Parameter()]
    [string]$InstallerOutputDir = '',

    [Parameter()]
    [string]$InstallerFileName = '',

    [Parameter()]
    [bool]$ValidatePayload = $true,

    [Parameter()]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

# Resolve paths
$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolvedPayloadRoot = if ([System.IO.Path]::IsPathRooted($PayloadRoot)) { $PayloadRoot } else { Join-Path $resolvedRepoRoot $PayloadRoot }
$resolvedNsisScriptPath = if ([System.IO.Path]::IsPathRooted($NsisScriptPath)) { $NsisScriptPath } else { Join-Path $resolvedRepoRoot $NsisScriptPath }
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $resolvedRepoRoot $OutputRoot }

if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) {
    $InstallerOutputDir = Join-Path $resolvedOutputRoot 'installer'
}
if ([string]::IsNullOrWhiteSpace($InstallerFileName)) {
    $InstallerFileName = "StorageWatchInstaller.$Version.exe"
}

$resolvedInstallerOutputDir = if ([System.IO.Path]::IsPathRooted($InstallerOutputDir)) { $InstallerOutputDir } else { Join-Path $resolvedRepoRoot $InstallerOutputDir }
$resolvedInstallerPath = Join-Path $resolvedInstallerOutputDir $InstallerFileName

# Validate required paths
if (-not (Test-Path -LiteralPath $resolvedNsisScriptPath)) {
    throw "NSIS script not found: $resolvedNsisScriptPath"
}

if (-not (Test-Path -LiteralPath $resolvedPayloadRoot)) {
    throw "Payload root not found: $resolvedPayloadRoot"
}

# Validate payload structure
if ($ValidatePayload) {
    $required = @('Agent', 'Server', 'UI')
    foreach ($dir in $required) {
        $path = Join-Path $resolvedPayloadRoot $dir
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required payload directory missing: $path"
        }

        $updaterPath = Join-Path $path 'updater\StorageWatch.Updater.exe'
        if (-not (Test-Path -LiteralPath $updaterPath)) {
            throw "Required updater executable missing from payload: $updaterPath"
        }
    }
}

Ensure-Directory -Path $resolvedInstallerOutputDir

if ((Test-Path -LiteralPath $resolvedInstallerPath) -and (-not $Force)) {
    throw "Installer already exists: $resolvedInstallerPath. Use -Force to overwrite."
}

# --- WhatIf / ShouldProcess block ---
if (-not $PSCmdlet.ShouldProcess("NSIS installer build", "Build installer")) {
    Write-Host "[WhatIf] Would build NSIS installer from '$resolvedNsisScriptPath'."
    Write-Host "[WhatIf] Would use payload root '$resolvedPayloadRoot'."
    Write-Host "[WhatIf] Would output installer to '$resolvedInstallerPath'."
    return
}

# --- NSIS auto-detection ---
if (-not (Get-Command $NsisExePath -ErrorAction SilentlyContinue)) {
    $defaultNsis = "C:\Program Files (x86)\NSIS\makensis.exe"
    if (Test-Path -LiteralPath $defaultNsis) {
        Write-Host "makensis not found on PATH. Using default NSIS path: $defaultNsis"
        $NsisExePath = $defaultNsis
    }
    else {
        throw "makensis.exe not found. Provide -NsisExePath or ensure NSIS is installed."
    }
}

# Remove existing installer if overwriting
if (Test-Path -LiteralPath $resolvedInstallerPath) {
    Remove-Item -LiteralPath $resolvedInstallerPath -Force
}

# Prepare NSIS working directory
$nsisWorkingDir = Split-Path -Parent $resolvedNsisScriptPath
$generatedInstallerName = 'StorageWatchInstaller.exe'
$generatedInstallerPath = Join-Path $nsisWorkingDir $generatedInstallerName

if (Test-Path -LiteralPath $generatedInstallerPath) {
    Remove-Item -LiteralPath $generatedInstallerPath -Force
}

# Build NSIS arguments
$nsisArgs = @(
    "/DVERSION=$Version",
    "/DCHANNEL=$Channel",
    "/DPAYLOAD_DIR=$resolvedPayloadRoot",
    $resolvedNsisScriptPath
)

# Execute NSIS
Push-Location $nsisWorkingDir
try {
    & $NsisExePath @nsisArgs
    if ($LASTEXITCODE -ne 0) {
        throw "NSIS build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

# Validate output
if (-not (Test-Path -LiteralPath $generatedInstallerPath)) {
    throw "NSIS completed but installer was not found at: $generatedInstallerPath"
}

# Move installer to final output location
Move-Item -LiteralPath $generatedInstallerPath -Destination $resolvedInstallerPath -Force

# Output metadata
[pscustomobject]@{
    Version       = $Version
    Channel       = $Channel
    InstallerPath = $resolvedInstallerPath
    PayloadRoot   = $resolvedPayloadRoot
    NsisScript    = $resolvedNsisScriptPath
}