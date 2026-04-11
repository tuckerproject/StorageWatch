[CmdletBinding()]
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

    [Parameter(Mandatory = $true)]
    [string]$BaseDownloadUrl,

    [Parameter(Mandatory = $true)]
    [string]$HashesFile,

    [Parameter(Mandatory = $true)]
    [string]$AgentPackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ServerPackagePath,

    [Parameter(Mandatory = $true)]
    [string]$UiPackagePath,

    [Parameter(Mandatory = $true)]
    [string]$UpdaterExecutablePath,

    [Parameter()]
    [string]$PluginsMetadataFile = '',

    [Parameter()]
    [string]$ReleaseNotesUrl = '',

    [Parameter()]
    [int]$ManifestVersion = 1,

    [Parameter()]
    [string]$ManifestOutputFile = '',

    [Parameter()]
    [bool]$EmitChannelSpecificPath = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ExistingPath([string]$Path, [string]$Name) {
    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $resolvedRepoRoot $Path }
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "$Name not found: $candidate"
    }
    return (Resolve-Path -LiteralPath $candidate).Path
}

function New-DownloadUrl([string]$BaseUrl, [string]$FileName) {
    return ('{0}/{1}' -f $BaseUrl.TrimEnd('/'), $FileName)
}

function Get-HashForPath([string]$Path, [array]$Entries) {
    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $fileName = Split-Path -Leaf $fullPath

    $hit = $Entries | Where-Object { $_.path -eq $fullPath } | Select-Object -First 1
    if (-not $hit) {
        $hit = $Entries | Where-Object { (Split-Path -Leaf $_.path) -eq $fileName } | Select-Object -First 1
    }

    if (-not $hit) {
        throw "Hash not found for: $fullPath"
    }

    return $hit.hash
}

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $resolvedRepoRoot $OutputRoot }

$resolvedHashesFile = Resolve-ExistingPath -Path $HashesFile -Name 'HashesFile'
$resolvedAgentPackagePath = Resolve-ExistingPath -Path $AgentPackagePath -Name 'AgentPackagePath'
$resolvedServerPackagePath = Resolve-ExistingPath -Path $ServerPackagePath -Name 'ServerPackagePath'
$resolvedUiPackagePath = Resolve-ExistingPath -Path $UiPackagePath -Name 'UiPackagePath'
$resolvedUpdaterExecutablePath = Resolve-ExistingPath -Path $UpdaterExecutablePath -Name 'UpdaterExecutablePath'

$resolvedPluginsMetadataFile = ''
if (-not [string]::IsNullOrWhiteSpace($PluginsMetadataFile)) {
    $resolvedPluginsMetadataFile = Resolve-ExistingPath -Path $PluginsMetadataFile -Name 'PluginsMetadataFile'
}

$hashEntries = Get-Content -LiteralPath $resolvedHashesFile -Raw | ConvertFrom-Json
if ($null -eq $hashEntries) {
    throw "Hashes file is empty or invalid JSON: $resolvedHashesFile"
}

if ([string]::IsNullOrWhiteSpace($ManifestOutputFile)) {
    if ($EmitChannelSpecificPath) {
        $ManifestOutputFile = Join-Path $resolvedOutputRoot ("manifest/$Channel/manifest.json")
    }
    else {
        $ManifestOutputFile = Join-Path $resolvedOutputRoot 'manifest/manifest.json'
    }
}

$resolvedManifestOutputFile = if ([System.IO.Path]::IsPathRooted($ManifestOutputFile)) { $ManifestOutputFile } else { Join-Path $resolvedRepoRoot $ManifestOutputFile }
$manifestDirectory = Split-Path -Parent $resolvedManifestOutputFile
if (-not (Test-Path -LiteralPath $manifestDirectory)) {
    New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
}

$agentFileName = Split-Path -Leaf $resolvedAgentPackagePath
$serverFileName = Split-Path -Leaf $resolvedServerPackagePath
$uiFileName = Split-Path -Leaf $resolvedUiPackagePath
$updaterFileName = Split-Path -Leaf $resolvedUpdaterExecutablePath

$agentHash = Get-HashForPath -Path $resolvedAgentPackagePath -Entries $hashEntries
$serverHash = Get-HashForPath -Path $resolvedServerPackagePath -Entries $hashEntries
$uiHash = Get-HashForPath -Path $resolvedUiPackagePath -Entries $hashEntries
$updaterHash = Get-HashForPath -Path $resolvedUpdaterExecutablePath -Entries $hashEntries

$plugins = @()
if (-not [string]::IsNullOrWhiteSpace($resolvedPluginsMetadataFile)) {
    $pluginMetadata = Get-Content -LiteralPath $resolvedPluginsMetadataFile -Raw | ConvertFrom-Json
    if ($pluginMetadata) {
        foreach ($plugin in $pluginMetadata) {
            if (-not $plugin.id) {
                continue
            }

            $pluginPackagePath = Resolve-ExistingPath -Path $plugin.packagePath -Name "Plugin package for '$($plugin.id)'"
            $pluginFileName = Split-Path -Leaf $pluginPackagePath
            $pluginHash = Get-HashForPath -Path $pluginPackagePath -Entries $hashEntries

            $pluginVersionValue = if ($null -ne $plugin.version -and -not [string]::IsNullOrWhiteSpace([string]$plugin.version)) { [string]$plugin.version } else { $Version }
            $pluginInfo = [ordered]@{
                id          = [string]$plugin.id
                version     = $pluginVersionValue
                downloadUrl = (New-DownloadUrl -BaseUrl $BaseDownloadUrl -FileName $pluginFileName)
                sha256      = [string]$pluginHash
            }

            if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesUrl)) {
                $pluginInfo['releaseNotesUrl'] = $ReleaseNotesUrl
            }

            $plugins += [pscustomobject]$pluginInfo
        }
    }
}

$agentInfo = [ordered]@{
    version     = $Version
    downloadUrl = (New-DownloadUrl -BaseUrl $BaseDownloadUrl -FileName $agentFileName)
    sha256      = $agentHash
}
$serverInfo = [ordered]@{
    version     = $Version
    downloadUrl = (New-DownloadUrl -BaseUrl $BaseDownloadUrl -FileName $serverFileName)
    sha256      = $serverHash
}
$uiInfo = [ordered]@{
    version     = $Version
    downloadUrl = (New-DownloadUrl -BaseUrl $BaseDownloadUrl -FileName $uiFileName)
    sha256      = $uiHash
}
$updaterInfo = [ordered]@{
    version     = $Version
    downloadUrl = (New-DownloadUrl -BaseUrl $BaseDownloadUrl -FileName $updaterFileName)
    sha256      = $updaterHash
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesUrl)) {
    $agentInfo['releaseNotesUrl'] = $ReleaseNotesUrl
    $serverInfo['releaseNotesUrl'] = $ReleaseNotesUrl
    $uiInfo['releaseNotesUrl'] = $ReleaseNotesUrl
}

$manifest = [ordered]@{
    manifestVersion = $ManifestVersion
    version         = $Version
    agent           = $agentInfo
    server          = $serverInfo
    ui              = $uiInfo
    updater         = $updaterInfo
    plugins         = $plugins
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedManifestOutputFile -Encoding UTF8

[pscustomobject]@{
    ManifestPath = (Resolve-Path -LiteralPath $resolvedManifestOutputFile).Path
    Version      = $Version
    Channel      = $Channel
    Plugins      = $plugins.Count
}
