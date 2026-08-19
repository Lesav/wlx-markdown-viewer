[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [Parameter(Mandatory = $true)]
    [string]$SignCommand
)

$ErrorActionPreference = 'Stop'

$securityModule = Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1'
Import-Module -Name $securityModule -ErrorAction Stop

$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
$resolvedSignCommand = (Resolve-Path -LiteralPath $SignCommand).Path

function Test-PortableExecutable {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) {
            return $false
        }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or ($peOffset + 4) -gt $stream.Length) {
            return $false
        }
        $stream.Position = $peOffset
        return $reader.ReadUInt32() -eq 0x00004550
    }
    finally {
        $reader.Dispose()
    }
}

$peFiles = @(Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse | Where-Object {
    Test-PortableExecutable -Path $_.FullName
})

$signedCount = 0
$skippedCount = 0
foreach ($file in $peFiles) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
        Write-Host "[package-sign] Already signed: $($file.FullName) [$($signature.Status)]"
        $skippedCount++
        continue
    }

    Write-Host "[package-sign] Signing: $($file.FullName)"
    & $resolvedSignCommand $file.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "sign.cmd failed with exit code $LASTEXITCODE for '$($file.FullName)'"
    }

    $verification = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($verification.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Signature verification failed for '$($file.FullName)': $($verification.StatusMessage)"
    }
    $signedCount++
}

Write-Host "[package-sign] PE files: $($peFiles.Count); signed: $signedCount; already signed: $skippedCount"
