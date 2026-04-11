[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [Parameter()]
    [string]$TimestampUrl = $(if ($env:STORAGEWATCH_SIGNING_TIMESTAMP_URL) { $env:STORAGEWATCH_SIGNING_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $FilePath)) {
    throw "File not found for signing: $FilePath"
}

$certBase64 = $env:STORAGEWATCH_SIGNING_CERT_BASE64
$certPassword = $env:STORAGEWATCH_SIGNING_CERT_PASSWORD

if ([string]::IsNullOrWhiteSpace($certBase64) -or [string]::IsNullOrWhiteSpace($certPassword)) {
    Write-Host "[SIGN] Signing skipped for '$FilePath' (certificate secrets not configured)."
    return $false
}

$tempPfxPath = Join-Path ([System.IO.Path]::GetTempPath()) ("storagewatch-signing-" + [guid]::NewGuid().ToString('N') + '.pfx')

try {
    [System.IO.File]::WriteAllBytes($tempPfxPath, [Convert]::FromBase64String($certBase64))

    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
    if (-not $signtool) {
        $candidates = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*\x64\signtool.exe' } |
            Sort-Object FullName -Descending
        $signtool = $candidates | Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $signtool) {
        throw 'signtool.exe was not found on this runner.'
    }

    & $signtool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $tempPfxPath /p $certPassword $FilePath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for '$FilePath' with exit code $LASTEXITCODE."
    }

    Write-Host "[SIGN] Signed '$FilePath'."
    return $true
}
finally {
    if (Test-Path -LiteralPath $tempPfxPath) {
        Remove-Item -LiteralPath $tempPfxPath -Force -ErrorAction SilentlyContinue
    }
}
