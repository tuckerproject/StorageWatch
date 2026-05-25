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
    [bool]$StageToPayload = $true,

    [Parameter()]
    [string]$PayloadRoot = (Join-Path $RepoRoot 'InstallerNSIS\Payload'),

    [Parameter()]
    [string]$PayloadUpdaterDir = '',

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

function Copy-DirectoryContent([string]$Source, [string]$Destination) {
    Ensure-Directory -Path $Destination
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $resolvedRepoRoot $ProjectPath }
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $resolvedRepoRoot $OutputRoot }
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

if ([string]::IsNullOrWhiteSpace($PayloadUpdaterDir)) {
    $PayloadUpdaterDir = Join-Path $PayloadRoot 'Updater'
}

$resolvedPublishDir = if ([System.IO.Path]::IsPathRooted($PublishDir)) { $PublishDir } else { Join-Path $resolvedRepoRoot $PublishDir }
$resolvedPackageOutputDir = if ([System.IO.Path]::IsPathRooted($PackageOutputDir)) { $PackageOutputDir } else { Join-Path $resolvedRepoRoot $PackageOutputDir }
$resolvedPayloadUpdaterDir = if ([System.IO.Path]::IsPathRooted($PayloadUpdaterDir)) { $PayloadUpdaterDir } else { Join-Path $resolvedRepoRoot $PayloadUpdaterDir }

if ((Test-Path -LiteralPath $resolvedPublishDir) -and (-not $Force)) {
    throw "Updater publish directory already exists: $resolvedPublishDir. Use -Force to overwrite."
}

if (-not $PSCmdlet.ShouldProcess('StorageWatch.Updater publish', 'Publish updater executable')) {
    Write-Host "[WhatIf] Would publish updater from '$resolvedProjectPath' to '$resolvedPublishDir'."
    if ($CopyToPackageOutput) {
        Write-Host "[WhatIf] Would package updater runtime as '$resolvedPackageOutputDir\StorageWatch.Updater.zip'."
    }
    if ($StageToPayload) {
        Write-Host "[WhatIf] Would stage updater payload to '$resolvedPayloadUpdaterDir'."
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
    "/p:Version=$Version",
    "/p:InformationalVersion=$Version"
)

Write-Host 'Publishing Updater...'
& dotnet @publishArgs | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Updater with exit code $LASTEXITCODE."
}

$updaterExe = Join-Path $resolvedPublishDir $ExecutableFileName
if (-not (Test-Path -LiteralPath $updaterExe)) {
    throw "Updater executable not found after publish: $updaterExe"
}

$packagedExecutablePath = $null
$packagedFolderPath = $null
$packagePath = $null
if ($CopyToPackageOutput) {
    Ensure-Directory -Path $resolvedPackageOutputDir
    $updaterPackageDir = Join-Path $resolvedPackageOutputDir 'StorageWatch.Updater'
    $packagePath = Join-Path $resolvedPackageOutputDir 'StorageWatch.Updater.zip'

    if ((Test-Path -LiteralPath $packagePath) -and (-not $Force)) {
        throw "Package already exists: $packagePath. Use -Force to overwrite."
    }

    # Copy entire publish folder
    if (Test-Path -LiteralPath $updaterPackageDir) {
        Remove-Item -LiteralPath $updaterPackageDir -Recurse -Force
    }
    Copy-Item -LiteralPath $resolvedPublishDir -Destination $updaterPackageDir -Recurse -Force
    $packagedFolderPath = $updaterPackageDir

    # Sign the EXE within the package folder
    $packagedExecutablePath = Join-Path $updaterPackageDir $ExecutableFileName
    if (Test-Path -LiteralPath $signScript) {
        $signed = & $signScript -FilePath $packagedExecutablePath
        if ($signed) {
            Ensure-Directory -Path $resolvedOutputRoot
            $markerFile = Join-Path $resolvedOutputRoot 'signing-performed.marker'
            Set-Content -LiteralPath $markerFile -Value '' -Encoding ASCII
            Write-Host "[SIGN] Signing marker written to '$markerFile'."
        } else {
            Write-Warning "[SIGN] Signing secrets not configured. Updater EXE was NOT signed. Smoke tests will skip Authenticode validation."
        }
    }

    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }

    Compress-Archive -Path (Join-Path $updaterPackageDir '*') -DestinationPath $packagePath -CompressionLevel Optimal -Force
}

if ($StageToPayload) {
    Clear-Directory -Path $resolvedPayloadUpdaterDir

    $updaterSourceForPayload = if ($packagedFolderPath -and (Test-Path -LiteralPath $packagedFolderPath)) {
        $packagedFolderPath
    } else {
        $resolvedPublishDir
    }

    Copy-DirectoryContent -Source $updaterSourceForPayload -Destination $resolvedPayloadUpdaterDir
}

$targetExeForInfo = if ($packagedExecutablePath -and (Test-Path -LiteralPath $packagedExecutablePath)) { $packagedExecutablePath } else { $updaterExe }
$updaterFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetExeForInfo).FileVersion
$updaterSha256 = (Get-FileHash -LiteralPath $targetExeForInfo -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Updater output path: $targetExeForInfo"
Write-Host "Updater EXE version: $updaterFileVersion"
Write-Host "Updater EXE SHA256: $updaterSha256"

[pscustomobject]@{
    Component = 'Updater'
    Version = $Version
    Channel = $Channel
    PublishDir = $resolvedPublishDir
    ExecutablePath = $updaterExe
    PackageOutputDir = if ($CopyToPackageOutput) { $resolvedPackageOutputDir } else { $null }
    PackagePath = if ($CopyToPackageOutput) { $packagePath } else { $null }
    PackagedFolderPath = $packagedFolderPath
    PackagedExecutablePath = $packagedExecutablePath
    PayloadDir = if ($StageToPayload) { $resolvedPayloadUpdaterDir } else { $null }
}
