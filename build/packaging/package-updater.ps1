[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter()]
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$ProjectPath = (Join-Path $RepoRoot 'StorageWatch.Updater\StorageWatch.Updater.csproj'),

    [Parameter()]
    [string]$Framework = 'net10.0',

    [Parameter()]
    [string]$RuntimeIdentifier = 'win-x64',

    [Parameter()]
    [bool]$SelfContained = $false,

    [Parameter()]
    [string]$OutputRoot = (Join-Path $RepoRoot 'artifacts'),

    [Parameter()]
    [string]$PublishDir = '',

    [Parameter()]
    [string]$PackageOutputDir = '',

    [Parameter()]
    [string]$ExecutableFileName = 'StorageWatch.Updater.exe',

    [Parameter()]
    [bool]$CopyToPackageOutput = $true,

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

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $resolvedRepoRoot $ProjectPath }
$signScript = Join-Path $resolvedRepoRoot 'build/packaging/sign-executable.ps1'

if (-not (Test-Path -LiteralPath $resolvedProjectPath)) {
    throw "Updater project file not found: $resolvedProjectPath"
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $OutputRoot 'publish/updater'
}

if ([string]::IsNullOrWhiteSpace($PackageOutputDir)) {
    $PackageOutputDir = Join-Path $OutputRoot 'packages/updater'
}

$resolvedPublishDir = if ([System.IO.Path]::IsPathRooted($PublishDir)) { $PublishDir } else { Join-Path $resolvedRepoRoot $PublishDir }
$resolvedPackageOutputDir = if ([System.IO.Path]::IsPathRooted($PackageOutputDir)) { $PackageOutputDir } else { Join-Path $resolvedRepoRoot $PackageOutputDir }

if ((Test-Path -LiteralPath $resolvedPublishDir) -and (-not $Force)) {
    throw "Updater publish directory already exists: $resolvedPublishDir. Use -Force to overwrite."
}

if (-not $PSCmdlet.ShouldProcess('StorageWatch.Updater publish', 'Publish updater executable')) {
    Write-Host "[WhatIf] Would publish updater from '$resolvedProjectPath' to '$resolvedPublishDir'."
    if ($CopyToPackageOutput) {
        Write-Host "[WhatIf] Would copy updater executable to '$resolvedPackageOutputDir'."
    }
    return
}

Clear-Directory -Path $resolvedPublishDir

$publishArgs = @(
    'publish',
    $resolvedProjectPath,
    '--configuration', $Configuration,
    '--framework', $Framework,
    '--runtime', $RuntimeIdentifier,
    '--self-contained', ($SelfContained.ToString().ToLowerInvariant()),
    '--output', $resolvedPublishDir,
    '/p:PublishSingleFile=true',
    '/p:IncludeNativeLibrariesForSelfExtract=false',
    "/p:Version=$Version",
    "/p:InformationalVersion=$Version"
)

Write-Host 'Publishing Updater...'
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Updater with exit code $LASTEXITCODE."
}

$updaterExe = Join-Path $resolvedPublishDir $ExecutableFileName
if (-not (Test-Path -LiteralPath $updaterExe)) {
    throw "Updater executable not found after publish: $updaterExe"
}

if (Test-Path -LiteralPath $signScript) {
    & $signScript -FilePath $updaterExe | Out-Null
}

$updaterFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($updaterExe).FileVersion
$updaterSha256 = (Get-FileHash -LiteralPath $updaterExe -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Updater output path: $updaterExe"
Write-Host "Updater EXE version: $updaterFileVersion"
Write-Host "Updater EXE SHA256: $updaterSha256"

$packagedExecutablePath = $null
if ($CopyToPackageOutput) {
    Ensure-Directory -Path $resolvedPackageOutputDir
    $packagedExecutablePath = Join-Path $resolvedPackageOutputDir $ExecutableFileName
    Copy-Item -LiteralPath $updaterExe -Destination $packagedExecutablePath -Force
}

[pscustomobject]@{
    Component = 'Updater'
    Version = $Version
    PublishDir = $resolvedPublishDir
    ExecutablePath = $updaterExe
    PackageOutputDir = if ($CopyToPackageOutput) { $resolvedPackageOutputDir } else { $null }
    PackagedExecutablePath = $packagedExecutablePath
}
