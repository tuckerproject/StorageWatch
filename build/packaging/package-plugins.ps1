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

    [Parameter(Mandatory = $true)]
    [string[]]$PluginSourcePaths,

    [Parameter()]
    [string]$PluginSearchPattern = '*.dll',

    [Parameter()]
    [string[]]$IncludePluginIds = @(),

    [Parameter()]
    [string[]]$ExcludePluginIds = @(),

    [Parameter()]
    [string]$PluginVersion = '',

    [Parameter()]
    [string]$PackageOutputDir = '',

    [Parameter()]
    [bool]$PackageIndividually = $true,

    [Parameter()]
    [bool]$StageToPayload = $true,

    [Parameter()]
    [string]$PayloadRoot = (Join-Path $RepoRoot 'InstallerNSIS\Payload'),

    [Parameter()]
    [string]$PayloadPluginsDir = '',

    [Parameter()]
    [string]$PluginMetadataOutputFile = '',

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
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $resolvedRepoRoot $OutputRoot }

if ([string]::IsNullOrWhiteSpace($PackageOutputDir)) {
    $PackageOutputDir = Join-Path $resolvedOutputRoot 'packages/plugins'
}
if ([string]::IsNullOrWhiteSpace($PayloadPluginsDir)) {
    $PayloadPluginsDir = Join-Path $PayloadRoot 'Plugins'
}
if ([string]::IsNullOrWhiteSpace($PluginVersion)) {
    $PluginVersion = $Version
}
if ([string]::IsNullOrWhiteSpace($PluginMetadataOutputFile)) {
    $PluginMetadataOutputFile = Join-Path $PackageOutputDir 'plugins.metadata.json'
}

$resolvedPackageOutputDir = if ([System.IO.Path]::IsPathRooted($PackageOutputDir)) { $PackageOutputDir } else { Join-Path $resolvedRepoRoot $PackageOutputDir }
$resolvedPayloadPluginsDir = if ([System.IO.Path]::IsPathRooted($PayloadPluginsDir)) { $PayloadPluginsDir } else { Join-Path $resolvedRepoRoot $PayloadPluginsDir }
$resolvedMetadataOutputFile = if ([System.IO.Path]::IsPathRooted($PluginMetadataOutputFile)) { $PluginMetadataOutputFile } else { Join-Path $resolvedRepoRoot $PluginMetadataOutputFile }

# --- Skip plugin packaging entirely if no plugin source paths exist ---
$allMissing = $true
foreach ($source in $PluginSourcePaths) {
    $candidate = if ([System.IO.Path]::IsPathRooted($source)) { $source } else { Join-Path $resolvedRepoRoot $source }
    if (Test-Path -LiteralPath $candidate) {
        $allMissing = $false
        break
    }
}

if ($allMissing) {
    Write-Host "No plugin source directories found. Skipping plugin packaging."
    return
}
# --- End skip block ---

$resolvedPluginSources = @()
foreach ($source in $PluginSourcePaths) {
    $candidate = if ([System.IO.Path]::IsPathRooted($source)) { $source } else { Join-Path $resolvedRepoRoot $source }
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Plugin source path not found: $candidate"
    }
    $resolvedPluginSources += (Resolve-Path -LiteralPath $candidate).Path
}

$plugins = @()
foreach ($source in $resolvedPluginSources) {
    $plugins += Get-ChildItem -Path $source -Filter $PluginSearchPattern -File -Recurse
}

$plugins = $plugins | Sort-Object FullName -Unique

if ($plugins.Count -eq 0) {
    throw "No plugin files found using pattern '$PluginSearchPattern'."
}

if ($IncludePluginIds.Count -gt 0) {
    $includeSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($id in $IncludePluginIds) { [void]$includeSet.Add($id) }
    $plugins = $plugins | Where-Object { $includeSet.Contains($_.BaseName) }
}

if ($ExcludePluginIds.Count -gt 0) {
    $excludeSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($id in $ExcludePluginIds) { [void]$excludeSet.Add($id) }
    $plugins = $plugins | Where-Object { -not $excludeSet.Contains($_.BaseName) }
}

if ($plugins.Count -eq 0) {
    throw 'No plugins left after include/exclude filtering.'
}

Ensure-Directory -Path $resolvedOutputRoot
Ensure-Directory -Path $resolvedPackageOutputDir

if (-not $PSCmdlet.ShouldProcess("Action description", "Action name")) {
    Write-Host "[WhatIf] Would package $($plugins.Count) plugin DLL(s) to '$resolvedPackageOutputDir'."
    if ($StageToPayload) {
        Write-Host "[WhatIf] Would stage plugin DLL(s) to '$resolvedPayloadPluginsDir'."
    }
    Write-Host "[WhatIf] Would write plugin metadata file to '$resolvedMetadataOutputFile'."
    return
}

if ($StageToPayload) {
    Clear-Directory -Path $resolvedPayloadPluginsDir
}

$metadata = @()

if ($PackageIndividually) {
    foreach ($plugin in $plugins) {
        $pluginId = $plugin.BaseName
        $zipPath = Join-Path $resolvedPackageOutputDir ("{0}.{1}.zip" -f $pluginId, $PluginVersion)

        if (Test-Path -LiteralPath $zipPath) {
            if (-not $Force) {
                throw "Plugin package already exists: $zipPath. Use -Force to overwrite."
            }
            Remove-Item -LiteralPath $zipPath -Force
        }

        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) ("storagewatch-plugin-{0}" -f ([guid]::NewGuid().ToString('N')))
        Ensure-Directory -Path $stagingDir

        try {
            Copy-Item -LiteralPath $plugin.FullName -Destination (Join-Path $stagingDir $plugin.Name) -Force
            Compress-Archive -Path (Join-Path $stagingDir '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
        }
        finally {
            if (Test-Path -LiteralPath $stagingDir) {
                Remove-Item -LiteralPath $stagingDir -Recurse -Force
            }
        }

        if ($StageToPayload) {
            Copy-Item -LiteralPath $plugin.FullName -Destination (Join-Path $resolvedPayloadPluginsDir $plugin.Name) -Force
        }

        $metadata += [pscustomobject]@{
            id          = $pluginId
            version     = $PluginVersion
            packagePath = $zipPath
            fileName    = (Split-Path -Leaf $zipPath)
        }
    }
}
else {
    $zipPath = Join-Path $resolvedPackageOutputDir ("StorageWatchPlugins.{0}.zip" -f $PluginVersion)
    if (Test-Path -LiteralPath $zipPath) {
        if (-not $Force) {
            throw "Plugin package already exists: $zipPath. Use -Force to overwrite."
        }
        Remove-Item -LiteralPath $zipPath -Force
    }

    $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) ("storagewatch-plugins-{0}" -f ([guid]::NewGuid().ToString('N')))
    Ensure-Directory -Path $stagingDir

    try {
        foreach ($plugin in $plugins) {
            Copy-Item -LiteralPath $plugin.FullName -Destination (Join-Path $stagingDir $plugin.Name) -Force
            if ($StageToPayload) {
                Copy-Item -LiteralPath $plugin.FullName -Destination (Join-Path $resolvedPayloadPluginsDir $plugin.Name) -Force
            }

            $metadata += [pscustomobject]@{
                id          = $plugin.BaseName
                version     = $PluginVersion
                packagePath = $zipPath
                fileName    = (Split-Path -Leaf $zipPath)
            }
        }

        Compress-Archive -Path (Join-Path $stagingDir '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
    }
    finally {
        if (Test-Path -LiteralPath $stagingDir) {
            Remove-Item -LiteralPath $stagingDir -Recurse -Force
        }
    }
}

$metadataDirectory = Split-Path -Parent $resolvedMetadataOutputFile
Ensure-Directory -Path $metadataDirectory
$metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedMetadataOutputFile -Encoding UTF8

[pscustomobject]@{
    Version            = $Version
    Channel            = $Channel
    PluginCount        = $metadata.Count
    PackageOutputDir   = $resolvedPackageOutputDir
    PayloadPluginsDir  = if ($StageToPayload) { $resolvedPayloadPluginsDir } else { $null }
    MetadataOutputFile = $resolvedMetadataOutputFile
}
