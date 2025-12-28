<#
.SYNOPSIS
    Release script for FlaUiCli - creates GitHub releases with built artifacts.

.DESCRIPTION
    This script:
    1. Validates prerequisites (dotnet, gh CLI, clean git status)
    2. Determines version from git tags or user input
    3. Runs tests
    4. Builds self-contained single-file executable
    5. Creates release package
    6. Creates git tag and pushes to remote
    7. Creates GitHub release with artifact

.PARAMETER Version
    Explicit version to release (e.g., "0.0.1")

.PARAMETER Bump
    Auto-bump version: "patch", "minor", or "major"

.PARAMETER SkipTests
    Skip running tests (not recommended for releases)

.EXAMPLE
    .\release.ps1 -Version 0.0.1
    
.EXAMPLE
    .\release.ps1 -Bump patch
#>

param(
    [string]$Version,
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Success { param($Message) Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

# Get the solution root (parent of scripts folder)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir

Write-Info "Solution root: $SolutionRoot"

# Change to solution root
Push-Location $SolutionRoot

try {
    # ============================================
    # Step 1: Validate prerequisites
    # ============================================
    Write-Info "Checking prerequisites..."

    # Check dotnet
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI is not installed or not in PATH"
    }
    Write-Success "dotnet CLI found"

    # Check gh
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is not installed or not in PATH"
    }
    
    # Check gh auth
    $ghAuthStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not authenticated. Run 'gh auth login' first."
    }
    Write-Success "GitHub CLI authenticated"

    # Check clean git status
    $gitStatus = git status --porcelain
    if ($gitStatus) {
        Write-Error "Working directory is not clean. Please commit or stash changes first."
        Write-Host $gitStatus
        throw "Uncommitted changes detected"
    }
    Write-Success "Git working directory is clean"

    # ============================================
    # Step 2: Determine version
    # ============================================
    Write-Info "Determining version..."

    # Get latest tag
    $latestTag = git describe --tags --abbrev=0 --match "v*" 2>$null
    if ($latestTag) {
        $currentVersion = $latestTag.TrimStart('v')
        Write-Info "Latest tag: $latestTag (version: $currentVersion)"
    } else {
        $currentVersion = "0.0.0"
        Write-Info "No existing tags found, starting from $currentVersion"
    }

    # Parse current version
    $versionParts = $currentVersion.Split('.')
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2].Split('-')[0]  # Handle pre-release suffixes

    if ($Version) {
        # Use explicit version
        $newVersion = $Version
    } elseif ($Bump) {
        # Calculate new version based on bump type
        switch ($Bump) {
            "major" { $major++; $minor = 0; $patch = 0 }
            "minor" { $minor++; $patch = 0 }
            "patch" { $patch++ }
        }
        $newVersion = "$major.$minor.$patch"
    } else {
        throw "Please specify either -Version or -Bump parameter"
    }

    $newTag = "v$newVersion"
    Write-Info "New version: $newVersion (tag: $newTag)"

    # Check if tag already exists
    $existingTag = git tag -l $newTag
    if ($existingTag) {
        throw "Tag $newTag already exists. Use a different version."
    }
    Write-Success "Tag $newTag is available"

    # ============================================
    # Step 3: Run tests
    # ============================================
    if ($SkipTests) {
        Write-Warn "Skipping tests (not recommended for releases)"
    } else {
        Write-Info "Running tests..."
        dotnet test --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed. Fix the tests before releasing."
        }
        Write-Success "All tests passed"
    }

    # ============================================
    # Step 4: Build self-contained executable
    # ============================================
    Write-Info "Building release..."

    $publishDir = Join-Path $SolutionRoot "publish"
    $artifactDir = Join-Path $publishDir "flaui-$newVersion-win-x64"

    # Clean publish directory
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

    # Build and publish
    dotnet publish src/FlaUiCli/FlaUiCli.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:Version=$newVersion `
        -p:AssemblyVersion="$major.$minor.$patch.0" `
        -p:FileVersion="$major.$minor.$patch.0" `
        --output $artifactDir

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Success "Build completed"

    # Copy additional files
    Copy-Item (Join-Path $SolutionRoot "README.md") $artifactDir
    Copy-Item (Join-Path $SolutionRoot "LICENSE") $artifactDir

    # ============================================
    # Step 5: Create zip package
    # ============================================
    Write-Info "Creating release package..."

    $zipName = "flaui-$newVersion-win-x64.zip"
    $zipPath = Join-Path $publishDir $zipName

    # Create zip
    Compress-Archive -Path $artifactDir -DestinationPath $zipPath -Force
    Write-Success "Created: $zipPath"

    # Get file size
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Info "Package size: $([math]::Round($zipSize, 2)) MB"

    # ============================================
    # Step 6: Create git tag and push
    # ============================================
    Write-Info "Creating git tag $newTag..."

    git tag -a $newTag -m "Release $newVersion"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create git tag"
    }
    Write-Success "Created tag $newTag"

    Write-Info "Pushing tag to origin..."
    git push origin $newTag
    if ($LASTEXITCODE -ne 0) {
        # Clean up local tag if push fails
        git tag -d $newTag
        throw "Failed to push tag to origin"
    }
    Write-Success "Pushed tag to origin"

    # ============================================
    # Step 7: Create GitHub release
    # ============================================
    Write-Info "Creating GitHub release..."

    $releaseNotes = @"
## FlaUiCli $newVersion

Windows UI automation CLI for AI agents.

### Installation

1. Download ``$zipName``
2. Extract to a folder
3. Add the folder to your PATH (optional)
4. Run ``flaui --help`` to get started

### Requirements

- Windows 10/11 (x64)
- No additional dependencies (self-contained)

### Quick Start

``````bash
# Start the service and connect to an app
flaui connect --name "YourApp"

# Find and click a button
flaui element find --aid "SubmitButton" --first
flaui action click <element-id>

# Take a screenshot
flaui screenshot --output screenshot.png
``````
"@

    # Create release with gh
    gh release create $newTag $zipPath `
        --title "FlaUiCli $newVersion" `
        --notes $releaseNotes

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create GitHub release"
    }
    Write-Success "GitHub release created"

    # ============================================
    # Done!
    # ============================================
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host " Release $newVersion completed successfully!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Release URL: https://github.com/opstudio-eu/FlaUiCli/releases/tag/$newTag"
    Write-Host ""

} catch {
    Write-Error $_.Exception.Message
    exit 1
} finally {
    Pop-Location
}
