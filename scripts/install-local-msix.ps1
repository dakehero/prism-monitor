param(
    [string]$PackageDir,
    [switch]$Launch,
    [switch]$TrustOnly
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-Argument([string]$Value) {
    return '"' + ($Value -replace '"', '\"') + '"'
}

function Test-CertificateTrusted([string]$Thumbprint) {
    $trustedRoot = Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
        Where-Object Thumbprint -eq $Thumbprint

    return $null -ne $trustedRoot
}

function Import-SigningCertificate([string]$Path) {
    Write-Host "Trusting certificate: $Path"
    Import-Certificate -FilePath $Path -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
}

$scriptPath = $PSCommandPath
$scriptDirectory = Split-Path -Parent $scriptPath

if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    $scriptDirectoryPackage = Get-ChildItem -LiteralPath $scriptDirectory -Filter *.msix -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($scriptDirectoryPackage) {
        $PackageDir = $scriptDirectory
    } else {
        $repoRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)
        $localMsixRoot = Join-Path $repoRoot "artifacts\local-msix"
        $PackageDir = Get-ChildItem -LiteralPath $localMsixRoot -Directory |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
}

$PackageDir = (Resolve-Path -LiteralPath $PackageDir).Path
$cert = Get-ChildItem -LiteralPath $PackageDir -Filter *Root*.cer | Select-Object -First 1
if (-not $cert) {
    $cert = Get-ChildItem -LiteralPath $PackageDir -Filter *.cer | Select-Object -First 1
}
$msix = Get-ChildItem -LiteralPath $PackageDir -Filter *.msix | Select-Object -First 1

if (-not $cert) {
    throw "No .cer file found in $PackageDir"
}

if (-not $msix) {
    throw "No .msix file found in $PackageDir"
}

$certificate = Get-PfxCertificate -FilePath $cert.FullName
$isTrusted = Test-CertificateTrusted $certificate.Thumbprint

if (-not $isTrusted -and -not (Test-IsAdministrator)) {
    $hostPath = (Get-Process -Id $PID).Path
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Quote-Argument $scriptPath),
        "-PackageDir", (Quote-Argument $PackageDir),
        "-TrustOnly"
    )

    Write-Host "Requesting administrator rights to trust the MSIX signing certificate..."
    $process = Start-Process -FilePath $hostPath -ArgumentList ($arguments -join " ") -Verb RunAs -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        exit $process.ExitCode
    }

    $isTrusted = Test-CertificateTrusted $certificate.Thumbprint
    if (-not $isTrusted) {
        throw "The signing certificate was not trusted."
    }
}

if (-not $isTrusted) {
    Import-SigningCertificate $cert.FullName
}

if ($TrustOnly) {
    Write-Host "Certificate trusted."
    exit 0
}

Write-Host "Installing package: $($msix.FullName)"
try {
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
} catch {
    $existingPackage = Get-AppxPackage -Name dakehero.PrismMonitor
    if (-not $existingPackage) {
        throw
    }

    Write-Host "Removing existing package before reinstalling: $($existingPackage.PackageFullName)"
    Remove-AppxPackage -Package $existingPackage.PackageFullName
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
}

$package = Get-AppxPackage -Name dakehero.PrismMonitor
if (-not $package) {
    throw "Package installation completed, but dakehero.PrismMonitor was not found."
}

Write-Host "Installed: $($package.PackageFullName)"

if ($Launch) {
    Write-Host "Launching Prism Monitor..."
    Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!App"
}
