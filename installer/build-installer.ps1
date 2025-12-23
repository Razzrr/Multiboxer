# Build script for EQBZ Multiboxer installer
# Requires: Inno Setup installed (https://jrsoftware.org/isdl.php)

param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== EQBZ Multiboxer Installer Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host "Project Root: $ProjectRoot"

# Step 1: Publish the application
if (-not $SkipPublish) {
    Write-Host "`n[1/3] Publishing application..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    try {
        dotnet publish src/Multiboxer.App/Multiboxer.App.csproj `
            -c Release `
            -r win-x64 `
            --self-contained true `
            -o publish/win-x64

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed"
        }
    }
    finally {
        Pop-Location
    }
    Write-Host "Published to: $ProjectRoot\publish\win-x64" -ForegroundColor Green
}
else {
    Write-Host "`n[1/3] Skipping publish (using existing build)" -ForegroundColor Yellow
}

# Step 2: Find Inno Setup compiler
Write-Host "`n[2/3] Finding Inno Setup..." -ForegroundColor Yellow
$InnoPath = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $InnoPath) {
    Write-Host "ERROR: Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found: $InnoPath" -ForegroundColor Green

# Step 3: Create dist directory and build installer
Write-Host "`n[3/3] Building installer..." -ForegroundColor Yellow
$DistDir = Join-Path $ProjectRoot "dist"
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

$IssFile = Join-Path $PSScriptRoot "setup.iss"
& $InnoPath "/DMyAppVersion=$Version" $IssFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=== Build Complete ===" -ForegroundColor Green
    Write-Host "Installer created in: $DistDir" -ForegroundColor Cyan
    Get-ChildItem $DistDir -Filter "*.exe" | ForEach-Object {
        Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)"
    }
}
else {
    Write-Host "`nERROR: Inno Setup compilation failed!" -ForegroundColor Red
    exit 1
}
