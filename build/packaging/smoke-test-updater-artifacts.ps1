[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter()]
    [string]$ArtifactsRoot = '',

    [Parameter()]
    [string]$ManifestPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Resolve-PathRequired([string]$pathValue, [string]$name) {
    $candidate = if ([System.IO.Path]::IsPathRooted($pathValue)) { $pathValue } else { Join-Path $resolvedRepoRoot $pathValue }
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "$name not found: $candidate"
    }
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Assert-VersionMatch([string]$actualVersion, [string]$expectedVersion, [string]$sourceName) {
    if ([string]::IsNullOrWhiteSpace($actualVersion)) {
        throw "$sourceName version is empty."
    }

    $isMatch = $actualVersion -eq $expectedVersion -or
        $actualVersion.StartsWith($expectedVersion + '.', [System.StringComparison]::OrdinalIgnoreCase) -or
        $actualVersion.StartsWith($expectedVersion + '+', [System.StringComparison]::OrdinalIgnoreCase)

    if (-not $isMatch) {
        throw "$sourceName version '$actualVersion' does not match expected version '$expectedVersion'."
    }
}

function Assert-ZipContainsUpdater([string]$zipPath, [string]$label) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -ieq 'updater/StorageWatch.Updater.exe' } | Select-Object -First 1
        if (-not $entry) {
            throw "$label ZIP does not contain updater/StorageWatch.Updater.exe"
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-AuthenticodeValid([string]$filePath, [string]$label) {
    $sig = Get-AuthenticodeSignature -FilePath $filePath
    if ($sig.Status -ne 'Valid') {
        throw "$label is not signed with a valid Authenticode signature. Status: $($sig.Status)"
    }
}

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $resolvedRepoRoot 'artifacts'
}

$resolvedArtifactsRoot = Resolve-PathRequired -pathValue $ArtifactsRoot -name 'ArtifactsRoot'

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $manifestCandidates = Get-ChildItem (Join-Path $resolvedArtifactsRoot 'manifest') -Recurse -Filter manifest.json -File
    if ($manifestCandidates.Count -eq 0) {
        throw 'Manifest file was not found under artifacts/manifest.'
    }
    $ManifestPath = $manifestCandidates[0].FullName
}

$resolvedManifestPath = Resolve-PathRequired -pathValue $ManifestPath -name 'ManifestPath'

$uiZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/ui') -Filter *.zip -File | Select-Object -First 1).FullName
$agentZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/agent') -Filter *.zip -File | Select-Object -First 1).FullName
$serverZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/server') -Filter *.zip -File | Select-Object -First 1).FullName
$updaterExe = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/updater') -Filter StorageWatch.Updater.exe -File | Select-Object -First 1).FullName

if (-not $uiZip -or -not $agentZip -or -not $serverZip) {
    throw 'One or more component ZIP files were not found in artifacts/packages/{ui,agent,server}.'
}
if (-not $updaterExe) {
    throw 'Updater executable was not found in artifacts/packages/updater.'
}

Assert-ZipContainsUpdater -zipPath $uiZip -label 'UI'
Assert-ZipContainsUpdater -zipPath $agentZip -label 'Agent'
Assert-ZipContainsUpdater -zipPath $serverZip -label 'Server'

$payloadRoot = Join-Path $resolvedRepoRoot 'InstallerNSIS/Payload'
$requiredPayloadUpdaterPaths = @(
    (Join-Path $payloadRoot 'UI/updater/StorageWatch.Updater.exe'),
    (Join-Path $payloadRoot 'Agent/updater/StorageWatch.Updater.exe'),
    (Join-Path $payloadRoot 'Server/updater/StorageWatch.Updater.exe')
)

foreach ($payloadPath in $requiredPayloadUpdaterPaths) {
    if (-not (Test-Path -LiteralPath $payloadPath)) {
        throw "Updater executable missing from installer staging directory: $payloadPath"
    }
}

Assert-AuthenticodeValid -filePath $updaterExe -label 'Packaged updater executable'

$tempExtractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("storagewatch-updater-smoke-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempExtractRoot -Force | Out-Null
try {
    $zipMap = @{
        UI = $uiZip
        Agent = $agentZip
        Server = $serverZip
    }

    foreach ($key in $zipMap.Keys) {
        $targetDir = Join-Path $tempExtractRoot $key
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipMap[$key], $targetDir)
        $exeInZip = Join-Path $targetDir 'updater/StorageWatch.Updater.exe'
        if (-not (Test-Path -LiteralPath $exeInZip)) {
            throw "$key ZIP extraction did not produce updater executable at updater/StorageWatch.Updater.exe"
        }
        Assert-AuthenticodeValid -filePath $exeInZip -label "$key ZIP updater executable"
    }
}
finally {
    if (Test-Path -LiteralPath $tempExtractRoot) {
        Remove-Item -LiteralPath $tempExtractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
if (-not $manifest.updater) {
    throw 'Manifest does not contain updater object.'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.updater.version)) {
    throw 'Manifest updater.version is missing.'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.updater.sha256)) {
    throw 'Manifest updater.sha256 is missing.'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.updater.downloadUrl)) {
    throw 'Manifest updater.downloadUrl is missing.'
}

Assert-VersionMatch -actualVersion ([string]$manifest.updater.version) -expectedVersion $ExpectedVersion -sourceName 'Manifest updater'

$updaterFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($updaterExe).FileVersion
Assert-VersionMatch -actualVersion $updaterFileVersion -expectedVersion $ExpectedVersion -sourceName 'Updater executable'

Write-Host '[SMOKE] Updater artifact smoke tests passed.'
