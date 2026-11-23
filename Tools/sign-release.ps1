# Signs a built mRemoteNG.exe with the local self-signed release certificate.
# The private key is stored outside the repository, in %LOCALAPPDATA%\mRemoteNG-Signing.
# The matching public thumbprint is pinned in ForkUpdateChannel.ReleaseSigningThumbprint.
param(
    [Parameter(Mandatory = $true)][string]$ExePath
)

$ErrorActionPreference = 'Stop'
$certDir = Join-Path $env:LOCALAPPDATA 'mRemoteNG-Signing'
$pfx = Join-Path $certDir 'mremoteng-release.pfx'
$password = 'mremoteng-local-signing-only'
$expectedThumbprint = '30E9AA926B036B2B81C80E5D0447347C21BC812D'

if (-not (Test-Path $ExePath)) {
    throw "Executable not found: $ExePath"
}
if (-not (Test-Path $pfx)) {
    throw "Release signing certificate not found at $pfx. Generate it with New-SelfSignedCertificate before building a release."
}

$signtool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
if (-not (Test-Path $signtool)) {
    throw "signtool.exe not found. Install the Windows SDK (Windows Kits 10)."
}

& $signtool sign /f $pfx /p $password /fd SHA256 $ExePath | Out-Null

$actual = (Get-AuthenticodeSignature $ExePath).SignerCertificate.Thumbprint
if ($actual -ne $expectedThumbprint) {
    throw "Signed with unexpected thumbprint: $actual (expected $expectedThumbprint)"
}
Write-Host "Signed $ExePath with thumbprint $actual"
