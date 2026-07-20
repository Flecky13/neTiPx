$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = (Resolve-Path (Join-Path $ScriptDir "..\..\")).Path

$ProjectPath = Join-Path $RootDir "src\neTiPx.UI.Avalonia\neTiPx.UI.Avalonia.csproj"
$PublishDir = Join-Path $RootDir "publish\windows-x64"
$PackagesDir = Join-Path $RootDir "packages"
$ReleaseAssetsDir = Join-Path $RootDir "release-assets"
$NsisScript = Join-Path $RootDir ".github\scripts\neTiPx_Pakage.nsi"

New-Item -ItemType Directory -Path $PackagesDir -Force | Out-Null
New-Item -ItemType Directory -Path $ReleaseAssetsDir -Force | Out-Null

dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $PublishDir

$makensis = $null
try {
    $makensis = (Get-Command makensis -ErrorAction Stop).Source
} catch {
    $candidatePaths = @(
        "$env:ProgramFiles\NSIS\makensis.exe",
        "$env:ProgramFiles(x86)\NSIS\makensis.exe",
        "C:\ProgramData\chocolatey\bin\makensis.exe"
    )
    foreach ($candidate in $candidatePaths) {
        if (Test-Path $candidate) {
            $makensis = $candidate
            break
        }
    }
}

if (-not $makensis) {
    throw "makensis wurde nicht gefunden."
}

& $makensis "/DProjectRoot=$RootDir" $NsisScript

$setup = Get-ChildItem -Path $PackagesDir -Filter "*Setup*.exe" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $setup) {
    throw "Kein NSIS-Installer in '$PackagesDir' gefunden."
}

Copy-Item $setup.FullName (Join-Path $ReleaseAssetsDir $setup.Name) -Force
Write-Host "Windows release asset: $($setup.Name)"
