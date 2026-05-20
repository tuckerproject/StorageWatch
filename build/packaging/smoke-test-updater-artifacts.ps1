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
        # Check for full updater folder structure
        $requiredEntries = @(
            'updater/StorageWatch.Updater.exe',
            'updater/StorageWatch.Updater.dll'
        )
        foreach ($requiredEntry in $requiredEntries) {
            $entry = $zip.Entries | Where-Object { $_.FullName -ieq $requiredEntry } | Select-Object -First 1
            if (-not $entry) {
                throw "$label ZIP does not contain $requiredEntry (expected full updater folder)"
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-AuthenticodeValid([string]$filePath, [string]$label) {
    $sig = Get-AuthenticodeSignature -FilePath $filePath
    if ($sig.Status -eq 'Valid') { return }
    if ($sig.Status -eq 'NotSigned') {
        throw "$label is not Authenticode-signed (Status: NotSigned). Ensure signing secrets are configured and packaging runs signing."
    }
    throw "$label has an invalid Authenticode signature (Status: $($sig.Status))."
}

function Assert-ManifestComponent([object]$manifest, [string]$componentName) {
    $component = $manifest.$componentName
    if (-not $component) {
        throw "Manifest does not contain '$componentName' entry."
    }

    if ([string]::IsNullOrWhiteSpace([string]$component.version)) {
        throw "Manifest $componentName.version is missing."
    }
    if ([string]::IsNullOrWhiteSpace([string]$component.sha256)) {
        throw "Manifest $componentName.sha256 is missing."
    }
    if ([string]::IsNullOrWhiteSpace([string]$component.downloadUrl)) {
        throw "Manifest $componentName.downloadUrl is missing."
    }
}

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $resolvedRepoRoot 'artifacts'
}

$resolvedArtifactsRoot = Resolve-PathRequired -pathValue $ArtifactsRoot -name 'ArtifactsRoot'

$signingMarkerPath = Join-Path $resolvedArtifactsRoot 'signing-performed.marker'
$signingExpected = Test-Path -LiteralPath $signingMarkerPath
if ($signingExpected) {
    Write-Host '[SMOKE] Signing marker found. Authenticode validation will be enforced.'
} else {
    Write-Warning '[SMOKE] Signing marker not found (signing-performed.marker). Authenticode validation will be SKIPPED. This is expected when signing secrets are absent.'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $manifestCandidates = @(Get-ChildItem (Join-Path $resolvedArtifactsRoot 'manifest') -Recurse -Filter manifest.json -File)
    if ($manifestCandidates.Count -eq 0) {
        throw 'Manifest file was not found under artifacts/manifest.'
    }
    $ManifestPath = $manifestCandidates[0].FullName
}

$resolvedManifestPath = Resolve-PathRequired -pathValue $ManifestPath -name 'ManifestPath'

$uiZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/ui') -Filter *.zip -File | Select-Object -First 1).FullName
$agentZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/agent') -Filter *.zip -File | Select-Object -First 1).FullName
$serverZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/server') -Filter *.zip -File | Select-Object -First 1).FullName
$updaterZip = (Get-ChildItem (Join-Path $resolvedArtifactsRoot 'packages/updater') -Filter StorageWatch.Updater.zip -File | Select-Object -First 1).FullName

if (-not $uiZip -or -not $agentZip -or -not $serverZip) {
    throw 'One or more component ZIP files were not found in artifacts/packages/{ui,agent,server}.'
}
if (-not $updaterZip) {
    throw 'Updater ZIP was not found in artifacts/packages/updater.'
}

Assert-ZipContainsUpdater -zipPath $uiZip -label 'UI'
Assert-ZipContainsUpdater -zipPath $agentZip -label 'Agent'
Assert-ZipContainsUpdater -zipPath $serverZip -label 'Server'

$payloadRoot = Join-Path $resolvedRepoRoot 'InstallerNSIS/Payload'
$requiredPayloadUpdaterPaths = @(
    (Join-Path $payloadRoot 'UI/updater/StorageWatch.Updater.exe'),
    (Join-Path $payloadRoot 'UI/updater/StorageWatch.Updater.dll'),
    (Join-Path $payloadRoot 'Agent/updater/StorageWatch.Updater.exe'),
    (Join-Path $payloadRoot 'Agent/updater/StorageWatch.Updater.dll'),
    (Join-Path $payloadRoot 'Server/updater/StorageWatch.Updater.exe'),
    (Join-Path $payloadRoot 'Server/updater/StorageWatch.Updater.dll')
)

foreach ($payloadPath in $requiredPayloadUpdaterPaths) {
    if (-not (Test-Path -LiteralPath $payloadPath)) {
        throw "Updater file missing from installer staging directory: $payloadPath"
    }
}

# Extract and validate updater ZIP structure
$tempUpdaterExtract = Join-Path $tempExtractRoot 'updater-validation'
New-Item -ItemType Directory -Path $tempUpdaterExtract -Force | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($updaterZip, $tempUpdaterExtract)

$updaterExeFromZip = Join-Path $tempUpdaterExtract 'StorageWatch.Updater.exe'
$updaterDllFromZip = Join-Path $tempUpdaterExtract 'StorageWatch.Updater.dll'
$updaterDepsFromZip = Join-Path $tempUpdaterExtract 'StorageWatch.Updater.deps.json'
$updaterRuntimeConfigFromZip = Join-Path $tempUpdaterExtract 'StorageWatch.Updater.runtimeconfig.json'

if (-not (Test-Path -LiteralPath $updaterExeFromZip)) {
    throw "Updater ZIP does not contain StorageWatch.Updater.exe"
}
if (-not (Test-Path -LiteralPath $updaterDllFromZip)) {
    throw "Updater ZIP does not contain StorageWatch.Updater.dll"
}
if (-not (Test-Path -LiteralPath $updaterDepsFromZip)) {
    throw "Updater ZIP does not contain StorageWatch.Updater.deps.json"
}
if (-not (Test-Path -LiteralPath $updaterRuntimeConfigFromZip)) {
    throw "Updater ZIP does not contain StorageWatch.Updater.runtimeconfig.json"
}

if ($signingExpected) {
    Assert-AuthenticodeValid -filePath $updaterExeFromZip -label 'Updater executable (from ZIP)'
} else {
    Write-Warning "[SMOKE] Skipping Authenticode validation for updater executable (signing not performed)."
}

$componentZipExtractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("storagewatch-updater-smoke-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $componentZipExtractRoot -Force | Out-Null
try {
    $zipMap = @{
        UI = $uiZip
        Agent = $agentZip
        Server = $serverZip
    }

    foreach ($key in $zipMap.Keys) {
        $targetDir = Join-Path $componentZipExtractRoot $key
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipMap[$key], $targetDir)
        $exeInZip = Join-Path $targetDir 'updater/StorageWatch.Updater.exe'
        $dllInZip = Join-Path $targetDir 'updater/StorageWatch.Updater.dll'
        if (-not (Test-Path -LiteralPath $exeInZip)) {
            throw "$key ZIP extraction did not produce updater executable at updater/StorageWatch.Updater.exe"
        }
        if (-not (Test-Path -LiteralPath $dllInZip)) {
            throw "$key ZIP extraction did not produce updater DLL at updater/StorageWatch.Updater.dll"
        }
        if ($signingExpected) {
            Assert-AuthenticodeValid -filePath $exeInZip -label "$key ZIP updater executable"
        } else {
            Write-Warning "[SMOKE] Skipping Authenticode validation for $key ZIP updater executable (signing not performed)."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $componentZipExtractRoot) {
        Remove-Item -LiteralPath $componentZipExtractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $tempUpdaterExtract) {
        Remove-Item -LiteralPath $tempUpdaterExtract -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json

Assert-ManifestComponent -manifest $manifest -componentName 'ui'
Assert-ManifestComponent -manifest $manifest -componentName 'agent'
Assert-ManifestComponent -manifest $manifest -componentName 'server'
Assert-ManifestComponent -manifest $manifest -componentName 'updater'

Assert-VersionMatch -actualVersion ([string]$manifest.ui.version) -expectedVersion $ExpectedVersion -sourceName 'Manifest UI'
Assert-VersionMatch -actualVersion ([string]$manifest.agent.version) -expectedVersion $ExpectedVersion -sourceName 'Manifest Agent'
Assert-VersionMatch -actualVersion ([string]$manifest.server.version) -expectedVersion $ExpectedVersion -sourceName 'Manifest Server'
Assert-VersionMatch -actualVersion ([string]$manifest.updater.version) -expectedVersion $ExpectedVersion -sourceName 'Manifest updater'

Assert-VersionMatch -actualVersion ([string]$manifest.ui.version) -expectedVersion ([string]$manifest.updater.version) -sourceName 'Manifest UI vs updater'
Assert-VersionMatch -actualVersion ([string]$manifest.agent.version) -expectedVersion ([string]$manifest.updater.version) -sourceName 'Manifest Agent vs updater'
Assert-VersionMatch -actualVersion ([string]$manifest.server.version) -expectedVersion ([string]$manifest.updater.version) -sourceName 'Manifest Server vs updater'

# Verify updater packageType field
if ([string]::IsNullOrWhiteSpace([string]$manifest.updater.packageType)) {
    Write-Warning "[SMOKE] Manifest updater.packageType is not set (expected 'zip'). This may be legacy format."
} elseif (([string]$manifest.updater.packageType) -ne 'zip') {
    throw "Manifest updater.packageType is '$($manifest.updater.packageType)', expected 'zip'."
}

$updaterFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($updaterExeFromZip).FileVersion
Assert-VersionMatch -actualVersion $updaterFileVersion -expectedVersion ([string]$manifest.updater.version) -sourceName 'Updater executable vs manifest updater'

Write-Host '[SMOKE] Updater artifact smoke tests passed.'
