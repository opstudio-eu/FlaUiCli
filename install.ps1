<#
.SYNOPSIS
    Install script for FlaUiCli - Windows UI automation CLI for AI agents.

.DESCRIPTION
    Downloads and installs FlaUiCli from GitHub Releases.
    - Installs to %LOCALAPPDATA%\FlaUiCli
    - Adds to User PATH automatically
    - No admin rights required

.PARAMETER Version
    Specific version to install (e.g., "0.0.1"). If not specified, installs latest.

.PARAMETER Uninstall
    Remove FlaUiCli and clean up PATH.

.PARAMETER InstallDir
    Custom installation directory. Defaults to %LOCALAPPDATA%\FlaUiCli

.EXAMPLE
    # Install latest version
    irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1 | iex

.EXAMPLE
    # Install specific version
    & ([scriptblock]::Create((irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1))) -Version 0.0.1

.EXAMPLE
    # Uninstall
    & ([scriptblock]::Create((irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1))) -Uninstall
#>

param(
    [string]$Version,
    [switch]$Uninstall,
    [string]$InstallDir
)

$ErrorActionPreference = "Stop"

# Configuration
$RepoOwner = "opstudio-eu"
$RepoName = "FlaUiCli"
$ExeName = "flaui.exe"

# Default install directory
if (-not $InstallDir) {
    $InstallDir = Join-Path $env:LOCALAPPDATA "FlaUiCli"
}

# Colors for output
function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Success { param($Message) Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

function Get-LatestVersion {
    $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing
        return $release.tag_name.TrimStart('v')
    } catch {
        throw "Failed to fetch latest release info: $_"
    }
}

function Get-ReleaseDownloadUrl {
    param([string]$Ver)
    
    $tag = "v$Ver"
    $assetName = "flaui-$Ver-win-x64.zip"
    $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/tags/$tag"
    
    try {
        $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing
        $asset = $release.assets | Where-Object { $_.name -eq $assetName }
        
        if (-not $asset) {
            throw "Asset $assetName not found in release $tag"
        }
        
        return $asset.browser_download_url
    } catch {
        throw "Failed to fetch release $tag : $_"
    }
}

function Add-ToUserPath {
    param([string]$PathToAdd)
    
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    
    if ($currentPath -split ";" | Where-Object { $_ -eq $PathToAdd }) {
        Write-Info "Already in PATH"
        return $false
    }
    
    $newPath = if ($currentPath) { "$currentPath;$PathToAdd" } else { $PathToAdd }
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    
    # Also update current session
    $env:Path = "$env:Path;$PathToAdd"
    
    return $true
}

function Remove-FromUserPath {
    param([string]$PathToRemove)
    
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
    
    if (-not $currentPath) {
        return $false
    }
    
    $paths = $currentPath -split ";" | Where-Object { $_ -and $_ -ne $PathToRemove }
    $newPath = $paths -join ";"
    
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    
    return $true
}

function Install-FlaUiCli {
    param([string]$Ver)
    
    Write-Host ""
    Write-Host "FlaUiCli Installer" -ForegroundColor White
    Write-Host "==================" -ForegroundColor White
    Write-Host ""
    
    # Determine version
    if (-not $Ver) {
        Write-Info "Fetching latest release info..."
        $Ver = Get-LatestVersion
    }
    Write-Info "Version to install: $Ver"
    
    # Check existing installation
    $exePath = Join-Path $InstallDir $ExeName
    if (Test-Path $exePath) {
        try {
            $currentVersion = & $exePath version 2>$null
            if ($currentVersion -eq $Ver) {
                Write-Success "FlaUiCli $Ver is already installed"
                return
            }
            Write-Info "Upgrading from $currentVersion to $Ver"
        } catch {
            Write-Info "Reinstalling..."
        }
    }
    
    # Get download URL
    Write-Info "Fetching download URL..."
    $downloadUrl = Get-ReleaseDownloadUrl -Ver $Ver
    
    # Create temp directory
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "flaui-install-$(Get-Random)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    try {
        # Download
        $zipPath = Join-Path $tempDir "flaui.zip"
        Write-Info "Downloading flaui-$Ver-win-x64.zip..."
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
        
        # Extract to temp first
        $extractPath = Join-Path $tempDir "extract"
        Write-Info "Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
        
        # Find the actual content (might be in a subdirectory)
        $sourceDir = $extractPath
        $subDirs = Get-ChildItem -Path $extractPath -Directory
        if ($subDirs.Count -eq 1) {
            $sourceDir = $subDirs[0].FullName
        }
        
        # Ensure install directory exists
        if (-not (Test-Path $InstallDir)) {
            New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        }
        
        # Stop any running flaui service
        $service = Get-Process -Name "flaui" -ErrorAction SilentlyContinue
        if ($service) {
            Write-Info "Stopping running FlaUiCli service..."
            try {
                & $exePath service stop 2>$null
                Start-Sleep -Milliseconds 500
            } catch { }
            
            # Force kill if still running
            $service = Get-Process -Name "flaui" -ErrorAction SilentlyContinue
            if ($service) {
                $service | Stop-Process -Force
                Start-Sleep -Milliseconds 500
            }
        }
        
        # Copy files
        Write-Info "Installing to $InstallDir..."
        Copy-Item -Path (Join-Path $sourceDir "*") -Destination $InstallDir -Recurse -Force
        
        # Add to PATH
        Write-Info "Configuring PATH..."
        $pathAdded = Add-ToUserPath -PathToAdd $InstallDir
        if ($pathAdded) {
            Write-Success "Added to User PATH"
        }
        
        # Verify installation
        $installedExe = Join-Path $InstallDir $ExeName
        if (-not (Test-Path $installedExe)) {
            throw "Installation failed: $ExeName not found"
        }
        
        $installedVersion = & $installedExe version 2>$null
        
        Write-Host ""
        Write-Success "FlaUiCli $installedVersion installed successfully!"
        Write-Host ""
        Write-Host "Installation path: $InstallDir" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Run 'flaui --help' to get started." -ForegroundColor White
        Write-Host ""
        
        if ($pathAdded) {
            Write-Warn "Restart your terminal for PATH changes to take effect."
            Write-Host ""
        }
        
    } finally {
        # Cleanup temp directory
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Uninstall-FlaUiCli {
    Write-Host ""
    Write-Host "FlaUiCli Uninstaller" -ForegroundColor White
    Write-Host "====================" -ForegroundColor White
    Write-Host ""
    
    # Stop any running service
    $exePath = Join-Path $InstallDir $ExeName
    if (Test-Path $exePath) {
        Write-Info "Stopping FlaUiCli service..."
        try {
            & $exePath service stop 2>$null
            Start-Sleep -Milliseconds 500
        } catch { }
    }
    
    # Kill any remaining processes
    $service = Get-Process -Name "flaui" -ErrorAction SilentlyContinue
    if ($service) {
        $service | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
    
    # Remove from PATH
    Write-Info "Removing from PATH..."
    Remove-FromUserPath -PathToRemove $InstallDir | Out-Null
    
    # Remove installation directory
    if (Test-Path $InstallDir) {
        Write-Info "Removing $InstallDir..."
        Remove-Item -Path $InstallDir -Recurse -Force
    }
    
    Write-Host ""
    Write-Success "FlaUiCli uninstalled successfully!"
    Write-Host ""
}

# Main entry point
try {
    if ($Uninstall) {
        Uninstall-FlaUiCli
    } else {
        Install-FlaUiCli -Ver $Version
    }
} catch {
    Write-Err $_.Exception.Message
    exit 1
}
