[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$InputPaths,

    [Parameter()]
    [ValidateSet('SHA256')]
    [string]$Algorithm = 'SHA256',

    [Parameter(Mandatory = $true)]
    [string]$OutputFile,

    [Parameter()]
    [ValidateSet('json','txt')]
    [string]$Format = 'json',

    [Parameter()]
    [string]$BasePath = '',

    [Parameter()]
    [bool]$IncludeFileSize = $true,

    [Parameter()]
    [bool]$FailOnMissing = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-InputPath([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    if ($FailOnMissing) {
        throw "Input path not found: $Path"
    }

    return $null
}

$resolvedInputs = @()
foreach ($path in $InputPaths) {
    $resolved = Resolve-InputPath -Path $path
    if ($null -ne $resolved) {
        $resolvedInputs += $resolved
    }
}

if ($resolvedInputs.Count -eq 0) {
    throw 'No valid input files found to hash.'
}

$resolvedBasePath = ''
if (-not [string]::IsNullOrWhiteSpace($BasePath)) {
    if (-not (Test-Path -LiteralPath $BasePath)) {
        throw "BasePath not found: $BasePath"
    }
    $resolvedBasePath = (Resolve-Path -LiteralPath $BasePath).Path
}

function Get-RelativePathCompat([string]$BasePathValue, [string]$TargetPathValue) {
    if ([string]::IsNullOrWhiteSpace($BasePathValue)) {
        return (Split-Path -Leaf $TargetPathValue)
    }

    $method = [System.IO.Path].GetMethod('GetRelativePath', [type[]]@([string], [string]))
    if ($null -ne $method) {
        return [System.IO.Path]::GetRelativePath($BasePathValue, $TargetPathValue)
    }

    $baseUri = New-Object System.Uri(($BasePathValue.TrimEnd('\\') + '\\'))
    $targetUri = New-Object System.Uri($TargetPathValue)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\\')
}

$entries = @()
foreach ($file in ($resolvedInputs | Sort-Object -Unique)) {
    $hash = Get-FileHash -LiteralPath $file -Algorithm $Algorithm
    $relativePath = Get-RelativePathCompat -BasePathValue $resolvedBasePath -TargetPathValue $file

    $fileInfo = Get-Item -LiteralPath $file

    $entry = [ordered]@{
        path         = $file
        relativePath = $relativePath.Replace('\\', '/')
        algorithm    = $Algorithm
        hash         = $hash.Hash.ToLowerInvariant()
    }

    if ($IncludeFileSize) {
        $entry['sizeBytes'] = [int64]$fileInfo.Length
    }

    $entries += [pscustomobject]$entry
}

$outputDirectory = Split-Path -Parent $OutputFile
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

switch ($Format) {
    'json' {
        $entries | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputFile -Encoding UTF8
    }
    'txt' {
        $lines = $entries | ForEach-Object { "{0} *{1}" -f $_.hash, $_.relativePath }
        Set-Content -LiteralPath $OutputFile -Value $lines -Encoding UTF8
    }
}

[pscustomobject]@{
    OutputFile = (Resolve-Path -LiteralPath $OutputFile).Path
    Count      = $entries.Count
    Algorithm  = $Algorithm
    Format     = $Format
}
