$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = (Resolve-Path (Join-Path $ScriptDir "..\..\")).Path

$ProjectPath = Join-Path $RootDir "src\neTiPx.UI.Avalonia\neTiPx.UI.Avalonia.csproj"
$PublishDir = Join-Path $RootDir "publish\windows-x64"
$PackagesDir = Join-Path $RootDir "packages"
$ReleaseAssetsDir = Join-Path $RootDir "release-assets"
$NsisScript = Join-Path $RootDir ".github\scripts\neTiPx_Pakage.nsi"
$BuildPropsPath = Join-Path $RootDir "src\Directory.Build.props"

New-Item -ItemType Directory -Path $PackagesDir -Force | Out-Null
New-Item -ItemType Directory -Path $ReleaseAssetsDir -Force | Out-Null

Write-Host "📦 Starte dotnet publish für Windows x64..." -ForegroundColor Cyan
dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $PublishDir

Write-Host "✅ dotnet publish abgeschlossen" -ForegroundColor Green

Write-Host "📄 Lese Version aus $BuildPropsPath..." -ForegroundColor Cyan
[xml]$buildProps = Get-Content $BuildPropsPath
$version = $buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version konnte nicht aus '$BuildPropsPath' gelesen werden."
}
$installerVersion = "V$version"
Write-Host "✅ Version: $installerVersion" -ForegroundColor Green

Write-Host "🔨 Erstelle NSIS Installer..." -ForegroundColor Cyan
Write-Host "   NSIS Script: $NsisScript" -ForegroundColor Gray
Write-Host "   Project Root: $RootDir" -ForegroundColor Gray
Write-Host "   App Version: $installerVersion" -ForegroundColor Gray

$makensis = $null
try {
    $makensis = (Get-Command makensis -ErrorAction Stop).Source
    Write-Host "✅ makensis gefunden im PATH: $makensis" -ForegroundColor Green
} catch {
    Write-Host "⚠️ makensis nicht direkt im PATH gefunden, suche in Standard-Pfaden..." -ForegroundColor Yellow
    
    # Refresh PATH (wichtig nach choco install)
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    
    # Erneut versuchen nach PATH-Refresh
    try {
        $makensis = (Get-Command makensis -ErrorAction Stop).Source
        Write-Host "✅ makensis nach PATH-Refresh gefunden: $makensis" -ForegroundColor Green
    } catch {
        # Standard-Pfade durchsuchen
        $candidatePaths = @(
            "$env:ProgramFiles\NSIS\makensis.exe",
            "$env:ProgramFiles(x86)\NSIS\makensis.exe",
            "C:\ProgramData\chocolatey\bin\makensis.exe",
            "C:\Program Files\NSIS\makensis.exe",
            "C:\Program Files (x86)\NSIS\makensis.exe"
        )
        foreach ($candidate in $candidatePaths) {
            if (Test-Path $candidate) {
                $makensis = $candidate
                Write-Host "✅ makensis gefunden in: $makensis" -ForegroundColor Green
                break
            }
        }
    }
}

if (-not $makensis) {
    Write-Host "❌ makensis wurde nicht gefunden." -ForegroundColor Red
    Write-Host "Bitte NSIS installieren: choco install nsis -y" -ForegroundColor Red
    throw "makensis wurde nicht gefunden. Bitte NSIS installieren."
}

& $makensis "/DProjectRoot=$RootDir" "/DAppVersion=$installerVersion" $NsisScript

if ($LASTEXITCODE -ne 0) {
    throw "NSIS Installer-Erstellung fehlgeschlagen mit Exit-Code: $LASTEXITCODE"
}

Write-Host "✅ NSIS Installer erstellt" -ForegroundColor Green

Write-Host "📦 Suche Setup-Datei in $PackagesDir..." -ForegroundColor Cyan
$setup = Get-ChildItem -Path $PackagesDir -Filter "*Setup*.exe" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $setup) {
    throw "Kein NSIS-Installer in '$PackagesDir' gefunden."
}

Copy-Item $setup.FullName (Join-Path $ReleaseAssetsDir $setup.Name) -Force
Write-Host "✅ Windows release asset: $($setup.Name)" -ForegroundColor Green
Write-Host "📂 Kopiert nach: $ReleaseAssetsDir" -ForegroundColor Green
