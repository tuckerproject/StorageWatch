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
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$ProjectPath = (Join-Path $RepoRoot 'StorageWatchAgent\StorageWatchAgent.csproj'),

    [Parameter()]
    [string]$Framework = 'net10.0',

    [Parameter()]
    [string]$RuntimeIdentifier = '',

    [Parameter()]
    [bool]$SelfContained = $false,

    [Parameter()]
    [string]$PublishDir = '',

    [Parameter()]
    [string]$PackageOutputDir = '',

    [Parameter()]
    [string]$PackageFileName = '',

    [Parameter()]
    [bool]$StageToPayload = $true,

    [Parameter()]
    [string]$PayloadRoot = (Join-Path $RepoRoot 'InstallerNSIS\Payload'),

    [Parameter()]
    [string]$PayloadComponentDir = '',

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

function Clear-Directory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-DirectoryContent([string]$Source, [string]$Destination) {
    Ensure-Directory -Path $Destination
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $resolvedRepoRoot $ProjectPath }

if (-not (Test-Path -LiteralPath $resolvedProjectPath)) {
    throw "Project file not found: $resolvedProjectPath"
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $OutputRoot 'publish/agent'
}

if ([string]::IsNullOrWhiteSpace($PackageOutputDir)) {
    $PackageOutputDir = Join-Path $OutputRoot 'packages/agent'
}

if ([string]::IsNullOrWhiteSpace($PackageFileName)) {
    $PackageFileName = "StorageWatchAgent.$Version.zip"
}

if ([string]::IsNullOrWhiteSpace($PayloadComponentDir)) {
    $PayloadComponentDir = Join-Path $PayloadRoot 'Agent'
}

$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $resolvedRepoRoot $OutputRoot }
$resolvedPublishDir = if ([System.IO.Path]::IsPathRooted($PublishDir)) { $PublishDir } else { Join-Path $resolvedRepoRoot $PublishDir }
$resolvedPackageOutputDir = if ([System.IO.Path]::IsPathRooted($PackageOutputDir)) { $PackageOutputDir } else { Join-Path $resolvedRepoRoot $PackageOutputDir }
$resolvedPayloadComponentDir = if ([System.IO.Path]::IsPathRooted($PayloadComponentDir)) { $PayloadComponentDir } else { Join-Path $resolvedRepoRoot $PayloadComponentDir }
$packagePath = Join-Path $resolvedPackageOutputDir $PackageFileName

Ensure-Directory -Path $resolvedOutputRoot
Ensure-Directory -Path $resolvedPackageOutputDir

if ((Test-Path -LiteralPath $packagePath) -and (-not $Force)) {
    throw "Package already exists: $packagePath. Use -Force to overwrite."
}

if (-not $PSCmdlet.ShouldProcess("Agent publish", "Publish Agent")) {
    Write-Host "[WhatIf] Would publish Agent from '$resolvedProjectPath' to '$resolvedPublishDir'."
    Write-Host "[WhatIf] Would create update package '$packagePath'."
    if ($StageToPayload) {
        Write-Host "[WhatIf] Would stage Agent payload to '$resolvedPayloadComponentDir'."
    }
    return
}

Clear-Directory -Path $resolvedPublishDir

$publishArgs = @(
    'publish',
    $resolvedProjectPath,
    '--configuration', $Configuration,
    '--framework', $Framework,
    '--output', $resolvedPublishDir,
    "/p:Version=$Version",
    "/p:InformationalVersion=$Version"
)

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishArgs += @('--runtime', $RuntimeIdentifier)
}

$publishArgs += @('--self-contained', ($SelfContained.ToString().ToLowerInvariant()))

Write-Host "Publishing Agent..."
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Agent with exit code $LASTEXITCODE."
}

if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

Compress-Archive -Path (Join-Path $resolvedPublishDir '*') -DestinationPath $packagePath -CompressionLevel Optimal -Force

if ($StageToPayload) {
    Clear-Directory -Path $resolvedPayloadComponentDir
    Copy-DirectoryContent -Source $resolvedPublishDir -Destination $resolvedPayloadComponentDir
}

$result = [pscustomobject]@{
    Component      = 'Agent'
    Version        = $Version
    Channel        = $Channel
    PublishDir     = $resolvedPublishDir
    PackagePath    = $packagePath
    PayloadDir     = if ($StageToPayload) { $resolvedPayloadComponentDir } else { $null }
}

$result
