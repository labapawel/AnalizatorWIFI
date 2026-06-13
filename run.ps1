#Requires -Version 5.1
<#
.SYNOPSIS
    Uruchamia AnalizatorWiFi na Windows.
.PARAMETER Build
    Wymusza pełną kompilację przed uruchomieniem.
.PARAMETER Publish
    Publikuje self-contained build zamiast uruchamiać przez dotnet run.
#>
param(
    [switch]$Build,
    [switch]$Publish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir  = $PSScriptRoot
$Project    = Join-Path $ScriptDir "src\AnalizatorWiFi.UI\AnalizatorWiFi.UI.csproj"
$PublishDir = Join-Path $ScriptDir "publish\win-x64"

# --- Check .NET 9 ---
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "Nie znaleziono 'dotnet'. Zainstaluj .NET 9 SDK: https://dotnet.microsoft.com/download"
    exit 1
}

$sdkVersion = dotnet --version 2>&1
if ($sdkVersion -notmatch "^9\.") {
    Write-Warning "Aktywna wersja SDK: $sdkVersion. Wymagany .NET 9."
}

# --- Restore ---
Write-Host "Przywracanie pakietow..." -ForegroundColor Cyan
dotnet restore $Project
if ($LASTEXITCODE -ne 0) { Write-Error "Restore nie powiodl sie"; exit 1 }

if ($Publish) {
    # --- Publish self-contained ---
    Write-Host "Publikowanie (win-x64, self-contained)..." -ForegroundColor Cyan
    dotnet publish $Project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) { Write-Error "Publish nie powiodl sie"; exit 1 }

    $exe = Join-Path $PublishDir "AnalizatorWiFi.UI.exe"
    Write-Host "Opublikowano: $exe" -ForegroundColor Green
    Write-Host "Uruchamianie..." -ForegroundColor Cyan
    & $exe
}
else {
    # --- dotnet run (development) ---
    $config = if ($Build) { "Release" } else { "Debug" }
    Write-Host "Uruchamianie ($config)..." -ForegroundColor Cyan
    dotnet run --project $Project -c $config
}
