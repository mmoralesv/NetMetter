<#
.SYNOPSIS
    Builds the NetMetter MSIX bundle for Microsoft Store submission.

.DESCRIPTION
    Publishes the app self-contained for each architecture (an MSIX cannot depend on the .NET
    Desktop Runtime: no framework package exists for it), stages it next to the package manifest
    and logo assets, then packs one .msix per architecture and bundles them.

    Store submissions need no signature - the Store re-signs the package after certification.
    Pass -CertificateThumbprint to sign a build for sideload testing instead.

.EXAMPLE
    ./build/Package.ps1
.EXAMPLE
    ./build/Package.ps1 -Version 1.2.0 -Architecture x64
.EXAMPLE
    ./build/Package.ps1 -IdentityName 1234Publisher.NetMetter -Publisher "CN=ABCD1234-..." -PublisherDisplayName "Contoso"
#>
[CmdletBinding()]
param(
    # MAJOR.MINOR.PATCH; defaults to <Version> in src/NetMetter.csproj. The package version appends ".0".
    [string] $Version,

    [ValidateSet('x64', 'arm64')]
    [string[]] $Architecture = @('x64', 'arm64'),

    # Partner Center › Product management › Product identity. The defaults only work for local builds.
    [string] $IdentityName = $(if ($env:NETMETTER_IDENTITY_NAME) { $env:NETMETTER_IDENTITY_NAME } else { 'NetMetter.Dev' }),
    [string] $Publisher = $(if ($env:NETMETTER_PUBLISHER) { $env:NETMETTER_PUBLISHER } else { 'CN=NetMetter Dev' }),
    [string] $PublisherDisplayName = $(if ($env:NETMETTER_PUBLISHER_DISPLAY_NAME) { $env:NETMETTER_PUBLISHER_DISPLAY_NAME } else { 'NetMetter (dev build)' }),

    [string] $OutputDirectory,

    # Sign the bundle with a certificate from Cert:\CurrentUser\My (sideload testing only).
    [string] $CertificateThumbprint,

    # Run the Windows App Certification Kit against the bundle. Needs an elevated shell.
    [switch] $RunWack
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\NetMetter.csproj'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'out' }

function Get-SdkTool {
    param([Parameter(Mandatory)] [string] $Name)
    $binRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $tool = Get-ChildItem -LiteralPath $binRoot -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object { [version] $_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$Name" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
    if (-not $tool) {
        throw "$Name was not found. Install the Windows SDK (it ships makeappx, makepri and signtool)."
    }
    $tool
}

function Invoke-Tool {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [string[]] $Arguments)
    Write-Verbose "$Path $($Arguments -join ' ')"
    $output = & $Path @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        throw "$(Split-Path $Path -Leaf) failed with exit code $LASTEXITCODE."
    }
    $output | ForEach-Object { Write-Verbose $_ }
}

function Write-Manifest {
    param(
        [Parameter(Mandatory)] [string] $Destination,
        [Parameter(Mandatory)] [string] $PackageVersion,
        [Parameter(Mandatory)] [string] $Arch
    )
    $xml = Get-Content -LiteralPath (Join-Path $repoRoot 'packaging\AppxManifest.xml') -Raw
    $replacements = @{
        '{IdentityName}'         = $IdentityName
        '{Publisher}'            = $Publisher
        '{PublisherDisplayName}' = $PublisherDisplayName
        '{Version}'              = $PackageVersion
        '{Architecture}'         = $Arch
    }
    foreach ($token in $replacements.Keys) {
        $xml = $xml.Replace($token, [System.Security.SecurityElement]::Escape($replacements[$token]))
    }
    [System.IO.File]::WriteAllText($Destination, $xml, (New-Object System.Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------- version

if (-not $Version) {
    $Version = ([xml](Get-Content -LiteralPath $project)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be MAJOR.MINOR.PATCH (for example 1.2.0); the Store reserves the fourth field. Got '$Version'."
}
$fields = $Version.Split('.') | ForEach-Object { [int] $_ }
if ($fields[0] -lt 1) { throw 'The first version field cannot be 0 for a Store package.' }
if ($fields | Where-Object { $_ -gt 65535 }) { throw 'Version fields must be 65535 or lower.' }
$packageVersion = "$Version.0"

$makeappx = Get-SdkTool 'makeappx.exe'
$makepri = Get-SdkTool 'makepri.exe'

$work = Join-Path $repoRoot 'obj\package'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $work, $OutputDirectory -Force | Out-Null

Write-Host "NetMetter $packageVersion  ($($Architecture -join ', '))" -ForegroundColor Cyan
Write-Host "  identity : $IdentityName / $Publisher"

# ---------------------------------------------------------------- resource index
# resources.pri maps "Assets\Square44x44Logo.png" to the right scale-/targetsize- variant.
# It is built from the assets alone so the index does not cover the whole app payload.

$priStage = Join-Path $work 'pri'
New-Item -ItemType Directory -Path $priStage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\Assets') -Destination (Join-Path $priStage 'Assets') -Recurse
Write-Manifest -Destination (Join-Path $priStage 'AppxManifest.xml') -PackageVersion $packageVersion -Arch $Architecture[0]

$priConfig = Join-Path $work 'priconfig.xml'
Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/o')
Invoke-Tool $makepri @('new', '/pr', $priStage, '/cf', $priConfig,
    '/mn', (Join-Path $priStage 'AppxManifest.xml'), '/of', (Join-Path $work 'resources.pri'), '/o')
Write-Host '  resources.pri built'

# ---------------------------------------------------------------- publish, stage and pack

$packagesDir = Join-Path $work 'packages'
New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null

foreach ($arch in $Architecture) {
    $stage = Join-Path $work "stage\$arch"
    Invoke-Tool 'dotnet' @('publish', $project, '-c', 'Release', '-r', "win-$arch",
        '--self-contained', 'true', "-p:Version=$Version", '-o', $stage, '-nologo', '-v', 'quiet')

    Get-ChildItem -LiteralPath $stage -Filter *.pdb -File | Remove-Item -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\Assets') -Destination (Join-Path $stage 'Assets') -Recurse
    Copy-Item -LiteralPath (Join-Path $work 'resources.pri') -Destination $stage
    Write-Manifest -Destination (Join-Path $stage 'AppxManifest.xml') -PackageVersion $packageVersion -Arch $arch

    $msix = Join-Path $packagesDir "NetMetter_${packageVersion}_$arch.msix"
    Invoke-Tool $makeappx @('pack', '/d', $stage, '/p', $msix, '/o')
    Write-Host ("  {0,-5} packed  {1,6:N1} MB" -f $arch, ((Get-Item -LiteralPath $msix).Length / 1MB))
}

$bundle = Join-Path $OutputDirectory "NetMetter_$packageVersion.msixbundle"
Invoke-Tool $makeappx @('bundle', '/d', $packagesDir, '/p', $bundle, '/bv', $packageVersion, '/o')

# ---------------------------------------------------------------- sign and verify

if ($CertificateThumbprint) {
    $signtool = Get-SdkTool 'signtool.exe'
    Invoke-Tool $signtool @('sign', '/fd', 'SHA256', '/sha1', $CertificateThumbprint, '/s', 'My', $bundle)
    Write-Host "  signed with $CertificateThumbprint"
}

if ($RunWack) {
    $appcert = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\App Certification Kit\appcert.exe'
    if (-not (Test-Path -LiteralPath $appcert)) { throw 'The Windows App Certification Kit is not installed.' }
    $identity = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'The certification kit installs the package, so it needs an elevated shell (and a signed bundle).'
    }
    $report = Join-Path $OutputDirectory 'wack-report.xml'
    Invoke-Tool $appcert @('reset')
    Invoke-Tool $appcert @('test', '-appxpackagepath', $bundle, '-reportoutputpath', $report)
    $result = ([xml](Get-Content -LiteralPath $report)).REPORT.OVERALL_RESULT
    Write-Host "  certification kit: $result"
    if ($result -ne 'PASS') { throw "The Windows App Certification Kit reported $result. See $report." }
}

Write-Host ("Bundle: {0} ({1:N1} MB)" -f $bundle, ((Get-Item -LiteralPath $bundle).Length / 1MB)) -ForegroundColor Green
if (-not $CertificateThumbprint) {
    Write-Host 'Upload this bundle to Partner Center as is; the Store signs it during publishing.'
}
