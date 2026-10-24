<#
.SYNOPSIS
    Packages published Rewordly binaries into release archives.
.DESCRIPTION
    Creates zip and tar.gz archives for distribution with checksum verification.
.PARAMETER Version
    Version string to package. Defaults to Version from Rewordly.Desktop.csproj.
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$PubDir = Join-Path $RepoRoot "pub"
$DistDir = Join-Path $RepoRoot "dist"
$AssetsDir = Join-Path $RepoRoot "src" "Rewordly.Desktop" "Assets"
$PackagingDir = Join-Path $RepoRoot "packaging"
$ReadmeFile = Join-Path $RepoRoot "README.md"
$LicenseFile = Join-Path $RepoRoot "LICENSE"
$Csproj = Join-Path $RepoRoot "src" "Rewordly.Desktop" "Rewordly.Desktop.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path $Csproj) {
        $xml = [xml](Get-Content $Csproj)
        $Version = $xml.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "2.0.0"
    }
}

Write-Host "Packaging Rewordly version $Version..." -ForegroundColor Cyan

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Windows targets
foreach ($target in @('win-x64', 'win-arm64')) {
    $targetPub = Join-Path $PubDir $target
    if (Test-Path $targetPub) {
        Write-Host "==> Packaging $target..." -ForegroundColor Cyan
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("rewordly_pkg_" + [System.Guid]::NewGuid().ToString("N"))
        $innerDir = Join-Path $tempDir "Rewordly"
        New-Item -ItemType Directory -Path $innerDir -Force | Out-Null

        Copy-Item (Join-Path $targetPub "Rewordly.exe") $innerDir
        if (Test-Path $ReadmeFile) { Copy-Item $ReadmeFile $innerDir }
        if (Test-Path $LicenseFile) { Copy-Item $LicenseFile $innerDir }

        $zipPath = Join-Path $DistDir "Rewordly-$Version-$target.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path $innerDir -DestinationPath $zipPath -CompressionLevel Optimal
        Remove-Item $tempDir -Recurse -Force
        Write-Host "Created $zipPath" -ForegroundColor Green
    }
}

# macOS targets
foreach ($target in @('osx-x64', 'osx-arm64')) {
    $targetPub = Join-Path $PubDir $target
    if (Test-Path $targetPub) {
        Write-Host "==> Packaging $target..." -ForegroundColor Cyan
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("rewordly_pkg_" + [System.Guid]::NewGuid().ToString("N"))
        $appDir = Join-Path $tempDir "Rewordly.app"
        $macOsDir = Join-Path $appDir "Contents\MacOS"
        $resDir = Join-Path $appDir "Contents\Resources"
        New-Item -ItemType Directory -Path $macOsDir -Force | Out-Null
        New-Item -ItemType Directory -Path $resDir -Force | Out-Null

        Copy-Item (Join-Path $targetPub "Rewordly") $macOsDir
        Get-ChildItem -Path $targetPub -Filter "*.dylib" | ForEach-Object {
            Copy-Item $_.FullName $macOsDir
        }
        Copy-Item (Join-Path $AssetsDir "Rewordly.icns") $resDir
        Copy-Item (Join-Path $PackagingDir "Info.plist") (Join-Path $appDir "Contents\Info.plist")
        if (Test-Path $ReadmeFile) { Copy-Item $ReadmeFile $tempDir }
        if (Test-Path $LicenseFile) { Copy-Item $LicenseFile $tempDir }

        $zipPath = Join-Path $DistDir "Rewordly-$Version-$target.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

        # On Windows tar or 7z can preserve unix permissions if needed, or Compress-Archive
        if (Get-Command tar.exe -ErrorAction SilentlyContinue) {
            & tar.exe -a -c -f $zipPath -C $tempDir "Rewordly.app" "README.md"
        } else {
            Compress-Archive -Path (Get-ChildItem -Path $tempDir) -DestinationPath $zipPath -CompressionLevel Optimal
        }
        Remove-Item $tempDir -Recurse -Force
        Write-Host "Created $zipPath" -ForegroundColor Green
    }
}

# Linux targets
foreach ($target in @('linux-x64', 'linux-arm64')) {
    $targetPub = Join-Path $PubDir $target
    if (Test-Path $targetPub) {
        Write-Host "==> Packaging $target..." -ForegroundColor Cyan
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("rewordly_pkg_" + [System.Guid]::NewGuid().ToString("N"))
        $archiveName = "Rewordly-$Version-$target"
        $innerDir = Join-Path $tempDir $archiveName
        New-Item -ItemType Directory -Path $innerDir -Force | Out-Null

        Copy-Item (Join-Path $targetPub "Rewordly") $innerDir
        Copy-Item (Join-Path $AssetsDir "icon.png") (Join-Path $innerDir "rewordly.png")
        Copy-Item (Join-Path $PackagingDir "rewordly.desktop") $innerDir
        Copy-Item (Join-Path $PackagingDir "install-linux.sh") $innerDir
        if (Test-Path $ReadmeFile) { Copy-Item $ReadmeFile $innerDir }
        if (Test-Path $LicenseFile) { Copy-Item $LicenseFile $innerDir }

        $tarGzPath = Join-Path $DistDir "$archiveName.tar.gz"
        if (Test-Path $tarGzPath) { Remove-Item $tarGzPath -Force }

        if (Get-Command tar.exe -ErrorAction SilentlyContinue) {
            & tar.exe -czf $tarGzPath -C $tempDir $archiveName
            Write-Host "Created $tarGzPath" -ForegroundColor Green
        } else {
            Write-Warning "tar.exe not found. Skipping tar.gz packaging for $target."
        }
        Remove-Item $tempDir -Recurse -Force
    }
}

# Generate SHA256 checksums
$archives = Get-ChildItem -Path $DistDir -Include @("*.zip", "*.tar.gz") -File
if ($archives.Count -gt 0) {
    Write-Host "==> Generating SHA256 checksums..." -ForegroundColor Cyan
    $hashFile = Join-Path $DistDir "SHA256SUMS.txt"
    $lines = foreach ($file in $archives) {
        $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($file.Name)"
    }
    $lines | Set-Content -Path $hashFile -Encoding utf8
    Write-Host "Created $hashFile" -ForegroundColor Green
}

Write-Host "Packaging finished. Files in $DistDir :" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Format-Table Name, Length, LastWriteTime
