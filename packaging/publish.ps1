<#
.SYNOPSIS
    Publishes self-contained single-file binaries for Rewordly.
.DESCRIPTION
    Builds single-file releases across all supported runtimes (Windows, macOS, Linux).
.PARAMETER Targets
    Array of runtime identifiers to publish. Defaults to all six targets.
.PARAMETER Configuration
    Build configuration (default: Release).
#>
[CmdletBinding()]
param(
    [string[]]$Targets = @('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64', 'linux-x64', 'linux-arm64'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$Project = Join-Path $RepoRoot "src\Rewordly.Desktop\Rewordly.Desktop.csproj"
$PubDir = Join-Path $RepoRoot "pub"

$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Test-Path $PubDir)) {
    New-Item -ItemType Directory -Path $PubDir | Out-Null
}

foreach ($target in $Targets) {
    Write-Host "==> Publishing for $target..." -ForegroundColor Cyan
    $outDir = Join-Path $PubDir $target
    
    $args = @(
        'publish', $Project,
        '-c', $Configuration,
        '-r', $target,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=none',
        '-p:EnableWindowsTargeting=true',
        '-o', $outDir
    )
    
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish for $target (Exit code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
    Write-Host "==> Published $target successfully." -ForegroundColor Green
}

Write-Host "==> All target builds completed successfully." -ForegroundColor Green
