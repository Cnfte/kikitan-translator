param(
    [ValidateSet("patch", "minor", "major", "none")]
    [string]$Bump = "patch",
    [string]$SetVersion = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$propsPath = Join-Path $root "Directory.Build.props"
if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at $propsPath"
}

$propsContent = Get-Content -Raw -Encoding UTF8 $propsPath
$match = [regex]::Match($propsContent, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) {
    throw "Could not parse <Version>x.y.z</Version> from Directory.Build.props"
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value
$oldVersion = "$major.$minor.$patch"

if (-not [string]::IsNullOrWhiteSpace($SetVersion)) {
    if ($SetVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid -SetVersion format '$SetVersion'. Expected x.y.z"
    }
    $newVersion = $SetVersion
} else {
    switch ($Bump) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
        "none"  { }
    }
    $newVersion = "$major.$minor.$patch"
}

if ($newVersion -ne $oldVersion) {
    $updatedProps = [regex]::Replace($propsContent, '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>", 1)
    [System.IO.File]::WriteAllText($propsPath, $updatedProps, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "[BUILD] Version bumped: v$oldVersion -> v$newVersion" -ForegroundColor Cyan
} else {
    Write-Host "[BUILD] Building version: v$newVersion (no bump)" -ForegroundColor Cyan
}

# Clean stale hashed frontend assets before building
$resAssets = Join-Path $root "KikitanTranslator.Photino\Resources\wwwroot\assets"
$relDir = Join-Path $root "KikitanTranslator.Photino\bin\Release\net9.0\win-x64"
$binAssets = Join-Path $relDir "wwwroot\assets"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $resAssets, $binAssets

# Build Photino (auto-runs npm build, syncs wwwroot, builds Overlay, runs Velopack)
Write-Host "[BUILD] Building KikitanTranslator.Photino (Release)..." -ForegroundColor Cyan
dotnet build "KikitanTranslator.Photino\KikitanTranslator.Photino.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Photino Release build failed with exit code $LASTEXITCODE" }

# Package payload.zip and Portable ZIP
$outRelease = Join-Path $relDir "Release"
New-Item -ItemType Directory -Force -Path $outRelease | Out-Null
$payloadZip = Join-Path $root "KikitanTranslator.Installer\payload.zip"
$portableZip = Join-Path $outRelease "KikitanTranslator-win-Portable.zip"
Remove-Item -Force -ErrorAction SilentlyContinue $payloadZip, $portableZip

# Remove older Setup EXEs from Release folder
Get-ChildItem -Path $outRelease -Filter "KikitanTranslator-Setup-v*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$srcFull = (Resolve-Path $relDir).Path
$zipStream = [System.IO.File]::Open($payloadZip, [System.IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $srcFull -Recurse -File | Where-Object {
        $rel = $_.FullName.Substring($srcFull.Length + 1)
        -not $rel.StartsWith("Release\") -and -not $rel.EndsWith(".pdb")
    } | ForEach-Object {
        $rel = $_.FullName.Substring($srcFull.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archive.Dispose()
    $zipStream.Dispose()
}
Copy-Item -Force $payloadZip $portableZip

# Publish WPF Installer with the new version
Write-Host "[BUILD] Publishing KikitanTranslator.Installer v$newVersion..." -ForegroundColor Cyan
$installerPublishDir = Join-Path $root "KikitanTranslator.Installer\bin\Release\net9.0-windows\win-x64\publish"
dotnet publish "KikitanTranslator.Installer\KikitanTranslator.Installer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $installerPublishDir
if ($LASTEXITCODE -ne 0) { throw "Installer publish failed with exit code $LASTEXITCODE" }

$setupOut = Join-Path $outRelease "KikitanTranslator-Setup-v$newVersion.exe"
Copy-Item -Force (Join-Path $installerPublishDir "KikitanTranslator-Setup.exe") $setupOut

Write-Host "[BUILD] Release v$newVersion completed:" -ForegroundColor Green
Get-Item $setupOut, $portableZip | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}}, LastWriteTime | Format-Table -AutoSize
